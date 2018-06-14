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
using Microsoft.DotNet.Tools.Tool.Install;
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

        public void Restore(FilePath project,
            FilePath? nugetConfig = null,
            string verbosity = null)
        {
            string packageId;
            VersionRange versionRange;
            string targetFramework;
            DirectoryPath assetJsonOutput;
            try
            {
                // The mock installer wrote a mock project file containing id;version;framework;stageDirectory
                var contents = _fileSystem.File.ReadAllText(project.Value);
                var tokens = contents.Split(';');
                if (tokens.Length != 4)
                {
                    throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
                }

                packageId = tokens[0];
                versionRange = VersionRange.Parse(tokens[1]);
                targetFramework = tokens[2];
                assetJsonOutput = new DirectoryPath(tokens[3]);
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
                nugetConfig);

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
            FilePath? nugetConfig = null)
        {
            var allPackages = _feeds
                .Where(f =>
                {
                    if (nugetConfig != null)
                    {
                        return ExcludeOtherFeeds(nugetConfig, f);
                    }

                    return true;
                })
                .SelectMany(f => f.Packages)
                .Where(f => f.PackageId == packageId);

            var bestVersion = versionRange.FindBestMatch(allPackages.Select(p => NuGetVersion.Parse(p.Version)));

            var package = allPackages.Where(p => NuGetVersion.Parse(p.Version).Equals(bestVersion)).FirstOrDefault();

            if (package == null)
            {
                _reporter?.WriteLine($"Error: failed to restore package {packageId}.");
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            return package;
        }

        private static bool ExcludeOtherFeeds(FilePath? nugetConfig, MockFeed f)
        {
            return f.Type == MockFeedType.ImplicitAdditionalFeed
                   || (f.Type == MockFeedType.ExplicitNugetConfig && f.Uri == nugetConfig.Value.Value);
        }
    }
}
