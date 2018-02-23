// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Install.Tool;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ProjectRestorerMock : IProjectRestorer
    {
        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string FakeCommandName = "SimulatorCommand";
        public const string DefaultPackageName = "global.tool.console.demo";
        public const string DefaultPackageVersion = "1.0.4";

        private readonly IFileSystem _fileSystem;
        private readonly IReporter _reporter;
        private readonly List<MockFeed> _feeds;

        public ProjectRestorerMock(
            IFileSystem fileSystem,
            IReporter reporter = null,
            IEnumerable<MockFeed> feeds = null)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _reporter = reporter;

            _feeds = new List<MockFeed>();
            if (feeds == null)
            {
                _feeds.Add(new MockFeed
                    {
                        Type = MockFeedType.FeedFromLookUpNugetConfig,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = DefaultPackageName,
                                Version = DefaultPackageVersion
                            }
                        }
                    });
            }
            else
            {
                _feeds.AddRange(feeds);
            }
        }

        public void Restore(
            FilePath project,
            DirectoryPath assetJsonOutput,
            FilePath? nugetConfig = null,
            string source = null,
            string verbosity = null)
        {
            string packageId;
            VersionRange versionRange;
            string targetFramework;

            try
            {
                // The mock installer wrote a mock project file containing id:version:framework
                var contents = _fileSystem.File.ReadAllText(project.Value);
                var tokens = contents.Split(':');
                if (tokens.Length != 3)
                {
                    throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
                }

                packageId = tokens[0];
                versionRange = VersionRange.Parse(tokens[1]);
                targetFramework = tokens[2];
            }
            catch (IOException)
            {
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            var feedPackage = GetPackage(
                packageId,
                versionRange,
                nugetConfig,
                source);

            var packageVersion = feedPackage.Version;
            targetFramework = string.IsNullOrEmpty(targetFramework) ? "targetFramework" : targetFramework;

             var fakeExecutableSubDirectory = Path.Combine(
                packageId.ToLowerInvariant(),
                packageVersion.ToLowerInvariant(),
                "tools",
                targetFramework,
                "any");
            var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

            _fileSystem.Directory.CreateDirectory(Path.Combine(assetJsonOutput.Value, fakeExecutableSubDirectory));
            _fileSystem.File.CreateEmptyFile(Path.Combine(assetJsonOutput.Value, fakeExecutablePath));
            _fileSystem.File.WriteAllText(
                assetJsonOutput.WithFile("project.assets.json").Value,
                fakeExecutablePath);
        }

        private MockFeedPackage GetPackage(
            string packageId,
            VersionRange versionRange = null,
            FilePath? nugetConfig = null,
            string source = null)
        {
            var package = _feeds
                .Where(f => {
                    if (nugetConfig != null)
                    {
                        return f.Type == MockFeedType.ExplicitNugetConfig && f.Uri == nugetConfig.Value.Value;
                    }
                    if (source != null)
                    {
                        return f.Type == MockFeedType.Source && f.Uri == source;
                    }
                    return true;
                })
                .SelectMany(f => f.Packages)
                .Where(p => MatchPackage(p, packageId, versionRange)).OrderByDescending(p => p.Version)
                .FirstOrDefault();

            if (package == null)
            {
                if (_reporter != null)
                {
                    _reporter.WriteLine($"Error: failed to restore package {packageId}.");
                }
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            return package;
        }

        private static bool MatchPackage(MockFeedPackage p, string packageId, VersionRange versionRange)
        {
            if (string.Compare(p.PackageId, packageId, StringComparison.CurrentCultureIgnoreCase) != 0)
            {
                return false;
            }

            return versionRange == null ||
                   versionRange.FindBestMatch(new[] { NuGetVersion.Parse(p.Version) }) != null;
        }
    }
}
