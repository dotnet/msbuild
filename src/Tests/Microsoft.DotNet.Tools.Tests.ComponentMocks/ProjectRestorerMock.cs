// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ProjectRestorerMock : IProjectRestorer
    {
        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string DefaultToolCommandName = "SimulatorCommand";
        public const string DefaultPackageName = "global.tool.console.demo";
        public const string DefaultPackageVersion = "1.0.4";
        public const string FakeCommandSettingsFileName = "FakeDotnetToolSettings.json";

        private readonly IFileSystem _fileSystem;
        private readonly IReporter _reporter;
        private readonly List<MockFeed> _feeds;

        public ProjectRestorerMock(
            IFileSystem fileSystem,
            IReporter reporter = null,
            List<MockFeed> feeds = null)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _reporter = reporter;

            if (feeds == null)
            {
                _feeds = new List<MockFeed>();
                _feeds.Add(new MockFeed
                    {
                        Type = MockFeedType.FeedFromGlobalNugetConfig,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = DefaultPackageName,
                                Version = DefaultPackageVersion,
                                ToolCommandName = DefaultToolCommandName,
                            }
                        }
                    });
            }
            else
            {
                _feeds = feeds;
            }
        }

        public void Restore(FilePath project,
            PackageLocation packageLocation,
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
                packageLocation.NugetConfig,
                packageLocation.RootConfigDirectory);

            var packageVersion = feedPackage.Version;
            targetFramework = string.IsNullOrEmpty(targetFramework) ? "targetFramework" : targetFramework;

             var fakeExecutableSubDirectory = Path.Combine(
                packageId.ToLowerInvariant(),
                packageVersion.ToLowerInvariant(),
                "tools",
                targetFramework,
                Constants.AnyRid);
            var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

            _fileSystem.Directory.CreateDirectory(Path.Combine(assetJsonOutput.Value, fakeExecutableSubDirectory));
            _fileSystem.File.CreateEmptyFile(Path.Combine(assetJsonOutput.Value, fakeExecutablePath));
            _fileSystem.File.WriteAllText(
                assetJsonOutput.WithFile("project.assets.json").Value,
                fakeExecutablePath);
            _fileSystem.File.WriteAllText(
                assetJsonOutput.WithFile(FakeCommandSettingsFileName).Value,
                JsonSerializer.Serialize(new {Name = feedPackage.ToolCommandName}));
        }

        public MockFeedPackage GetPackage(
            string packageId,
            VersionRange versionRange,
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null)
        {
            var allPackages = _feeds
                .Where(feed =>
                {
                    if (nugetConfig == null)
                    {
                        return SimulateNugetSearchNugetConfigAndMatch(
                            rootConfigDirectory,
                            feed);
                    }
                    else
                    {
                        return ExcludeOtherFeeds(nugetConfig.Value, feed);
                    }
                })
                .SelectMany(f => f.Packages)
                .Where(f => f.PackageId == packageId)
                .ToList();

            var bestVersion = versionRange.FindBestMatch(allPackages.Select(p => NuGetVersion.Parse(p.Version)));

            var package = allPackages.FirstOrDefault(p => NuGetVersion.Parse(p.Version).Equals(bestVersion));

            if (package == null)
            {
                _reporter?.WriteLine($"Error: failed to restore package {packageId}.");
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }

            return package;
        }

        /// <summary>
        /// Simulate NuGet search nuget config from parent directories.
        /// Assume all nuget.config has Clear
        /// And then filter against mock feed
        /// </summary>
        private bool SimulateNugetSearchNugetConfigAndMatch(
            DirectoryPath? rootConfigDirectory,
            MockFeed feed)
        {
            if (rootConfigDirectory != null)
            {
                var probedNugetConfig = EnumerateDefaultAllPossibleNuGetConfig(rootConfigDirectory.Value)
                    .FirstOrDefault(possibleNugetConfig =>
                        _fileSystem.File.Exists(possibleNugetConfig.Value));

                if (!Equals(probedNugetConfig, default(FilePath)))
                {
                    return (feed.Type == MockFeedType.FeedFromLookUpNugetConfig) ||
                           (feed.Type == MockFeedType.ImplicitAdditionalFeed) ||
                           (feed.Type == MockFeedType.FeedFromLookUpNugetConfig
                            && feed.Uri == probedNugetConfig.Value);
                }
            }

            return feed.Type != MockFeedType.ExplicitNugetConfig
                    && feed.Type != MockFeedType.FeedFromLookUpNugetConfig;
        }

        private static IEnumerable<FilePath> EnumerateDefaultAllPossibleNuGetConfig(DirectoryPath probStart)
        {
            DirectoryPath? currentSearchDirectory = probStart;
            while (currentSearchDirectory.HasValue)
            {
                var tryNugetConfig = currentSearchDirectory.Value.WithFile("nuget.config");
                yield return tryNugetConfig;
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private static bool ExcludeOtherFeeds(FilePath nugetConfig, MockFeed f)
        {
            return f.Type == MockFeedType.ImplicitAdditionalFeed
                   || (f.Type == MockFeedType.ExplicitNugetConfig && f.Uri == nugetConfig.Value);
        }
    }
}
