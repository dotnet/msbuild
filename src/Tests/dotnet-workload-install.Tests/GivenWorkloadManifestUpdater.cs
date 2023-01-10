// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenWorkloadManifestUpdater : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestFileName = "WorkloadManifest.json";
        private readonly string _manifestSentinelFileName = ".workloadAdvertisingManifestSentinel";
        private readonly ManifestId[] _installedManifests;

        public GivenWorkloadManifestUpdater(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _installedManifests = new ManifestId[] { new ManifestId("test-manifest-1"), new ManifestId("test-manifest-2"), new ManifestId("test-manifest-3") };
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItCanUpdateAdvertisingManifests()
        {
            (var manifestUpdater, var nugetDownloader, _) = GetTestUpdater();

            manifestUpdater.UpdateAdvertisingManifestsAsync(true).Wait();
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(GetExpectedDownloadedPackages());
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItUpdatesWhenNoSentinelExists()
        {
            (var manifestUpdater, var nugetDownloader, var sentinelPath) = GetTestUpdater();

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(GetExpectedDownloadedPackages());
            File.Exists(sentinelPath).Should().BeTrue();
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItUpdatesWhenDue()
        {
            Func<string, string> getEnvironmentVariable = (envVar) => envVar.Equals(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS) ? "0" : string.Empty;
            (var manifestUpdater, var nugetDownloader, var sentinelPath) = GetTestUpdater(getEnvironmentVariable: getEnvironmentVariable);

            File.WriteAllText(sentinelPath, string.Empty);
            var createTime = DateTime.Now;

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();

            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(GetExpectedDownloadedPackages());
            File.Exists(sentinelPath).Should().BeTrue();
            File.GetLastAccessTime(sentinelPath).Should().BeAfter(createTime);
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItDoesNotUpdateWhenNotDue()
        {
            (var manifestUpdater, var nugetDownloader, var sentinelPath) = GetTestUpdater();

            File.Create(sentinelPath);
            var createTime = DateTime.Now;

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            nugetDownloader.DownloadCallParams.Should().BeEmpty();
            File.GetLastAccessTime(sentinelPath).Should().BeBefore(createTime);
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItHonorsDisablingEnvVar()
        {
            Func<string, string> getEnvironmentVariable = (envVar) => envVar.Equals(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE) ? "true" :  string.Empty;
            (var manifestUpdater, var nugetDownloader, _) = GetTestUpdater(getEnvironmentVariable: getEnvironmentVariable);

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            nugetDownloader.DownloadCallParams.Should().BeEmpty();
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItCanCalculateUpdates()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var expectedManifestUpdates = new ManifestVersionUpdate[] {
                new ManifestVersionUpdate(new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), featureBand, new ManifestVersion("7.0.0"), featureBand),
                new ManifestVersionUpdate(new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), featureBand, new ManifestVersion("4.0.0"), featureBand) };
            var expectedManifestNotUpdated = new ManifestId[] { new ManifestId("test-manifest-3"), new ManifestId("test-manifest-4") };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach (ManifestVersionUpdate manifestUpdate in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestUpdate.ManifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestUpdate.ManifestId.ToString(), _manifestFileName), GetManifestContent(manifestUpdate.ExistingVersion));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifestUpdate.ManifestId.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifestUpdate.ManifestId.ToString(), _manifestFileName), GetManifestContent(manifestUpdate.NewVersion));
            }
            foreach (var manifest in expectedManifestNotUpdated)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
            }

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.ManifestId)
                .Concat(expectedManifestNotUpdated)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, dotnetRoot);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, userProfileDir: Path.Combine(testDir, ".dotnet"), testDir, installationRepo);

            var manifestUpdates = manifestUpdater.CalculateManifestUpdates().Select( m => m.manifestUpdate);
            manifestUpdates.Should().BeEquivalentTo(expectedManifestUpdates);
        }

        [Fact]
        public void GivenWorkloadManifestRollbackItCanCalculateUpdates()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var expectedManifestUpdates = new ManifestVersionUpdate[] {
                new ManifestVersionUpdate(new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), featureBand, new ManifestVersion("4.0.0"), featureBand),
                new ManifestVersionUpdate(new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), featureBand, new ManifestVersion("2.0.0"), featureBand) };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach (var manifestUpdate in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestUpdate.ManifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestUpdate.ManifestId.ToString(), _manifestFileName), GetManifestContent(manifestUpdate.ExistingVersion));
            }

            var rollbackDefContent = JsonSerializer.Serialize(new Dictionary<string, string>() { { "test-manifest-1", "4.0.0" }, { "test-manifest-2", "2.0.0" } });
            var rollbackDefPath = Path.Combine(testDir, "testRollbackDef.txt");
            File.WriteAllText(rollbackDefPath, rollbackDefContent);

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.ManifestId)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, dotnetRoot);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            var manifestUpdates = manifestUpdater.CalculateManifestRollbacks(rollbackDefPath);
            manifestUpdates.Should().BeEquivalentTo(expectedManifestUpdates);
        }

        [Fact]
        public void GivenFromRollbackDefinitionItErrorsOnInstalledExtraneousManifestId()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var expectedManifestUpdates = new (ManifestId, ManifestVersion, ManifestVersion)[] {
                (new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), new ManifestVersion("4.0.0")),
                (new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), new ManifestVersion("2.0.0")) };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach ((var manifestId, var existingVersion, _) in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(existingVersion));
            }

            // Write extraneous manifest that the rollback definition will not have
            Directory.CreateDirectory(Path.Combine(installedManifestDir, "test-manifest-3"));
            File.WriteAllText(Path.Combine(installedManifestDir, "test-manifest-3", _manifestFileName), GetManifestContent(new ManifestVersion("1.0.0")));

            var rollbackDefContent = JsonSerializer.Serialize(new Dictionary<string, string>() { { "test-manifest-1", "4.0.0" }, { "test-manifest-2", "2.0.0" } });
            var rollbackDefPath = Path.Combine(testDir, "testRollbackDef.txt");
            File.WriteAllText(rollbackDefPath, rollbackDefContent);

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.Item1)
                .Append(new ManifestId("test-manifest-3"))
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(Array.Empty<string>()), dotnetRoot);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            manifestUpdater.CalculateManifestRollbacks(rollbackDefPath);
            string.Join(" ", _reporter.Lines).Should().Contain(rollbackDefPath);
        }

        [Fact]
        public void GivenFromRollbackDefinitionItErrorsOnExtraneousManifestIdInRollbackDefinition()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var expectedManifestUpdates = new (ManifestId, ManifestVersion, ManifestVersion)[] {
                (new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), new ManifestVersion("4.0.0")),
                (new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), new ManifestVersion("2.0.0")) };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach ((var manifestId, var existingVersion, _) in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(existingVersion));
            }

            var rollbackDefContent = JsonSerializer.Serialize(new Dictionary<string, string>() {
                { "test-manifest-1", "4.0.0" },
                { "test-manifest-2", "2.0.0" },
                { "test-manifest-3", "1.0.0" } // This manifest is not installed, should cause an error
            });
            var rollbackDefPath = Path.Combine(testDir, "testRollbackDef.txt");
            File.WriteAllText(rollbackDefPath, rollbackDefContent);

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.Item1)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(Array.Empty<string>()), dotnetRoot);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            manifestUpdater.CalculateManifestRollbacks(rollbackDefPath);
            string.Join(" ", _reporter.Lines).Should().Contain(rollbackDefPath);
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItChoosesHighestManifestVersionInCache()
        {
            var manifestId = "mock-manifest";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");

            // Write mock manifest
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(adManifestDir);
            Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId));
            File.WriteAllText(Path.Combine(installedManifestDir, manifestId, _manifestFileName), GetManifestContent(new ManifestVersion("1.0.0")));

            // Write multiple manifest packages to the offline cache
            var offlineCache = Path.Combine(testDir, "cache");
            Directory.CreateDirectory(offlineCache);
            File.Create(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.2.0.0.nupkg"));
            File.Create(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.3.0.0.nupkg"));

            var workloadManifestProvider = new MockManifestProvider(new string[] { Path.Combine(installedManifestDir, manifestId, _manifestFileName) });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, dotnetRoot);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);
            manifestUpdater.UpdateAdvertisingManifestsAsync(false, new DirectoryPath(offlineCache)).Wait();

            // We should have chosen the higher version manifest package to install/ extract
            nugetDownloader.ExtractCallParams.Count().Should().Be(1);
            nugetDownloader.ExtractCallParams[0].Item1.Should().Be(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.3.0.0.nupkg"));
        }

        [Theory]
        [InlineData("build", true)]
        [InlineData("publish", true)]
        [InlineData("run", false)]
        public void GivenWorkloadsAreOutOfDateUpdatesAreAdvertisedOnRestoringCommands(string commandName, bool shouldShowUpdateNotification)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: commandName)
                .WithSource()
                .Restore(Log);
            var sdkFeatureBand = new SdkFeatureBand(TestContext.Current.ToolsetUnderTest.SdkVersion);
            // Write fake updates file
            Directory.CreateDirectory(Path.Combine(testInstance.Path, ".dotnet"));
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", $".workloadAdvertisingUpdates{sdkFeatureBand}"), @"[""maui""]");
            // Don't check for updates again and overwrite our existing updates file
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", $".workloadAdvertisingManifestSentinel{sdkFeatureBand}"), string.Empty);

            var command = new DotnetCommand(Log);
            var commandResult = command
                .WithEnvironmentVariable("DOTNET_CLI_HOME", testInstance.Path)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(commandName);

            commandResult
                .Should()
                .Pass();

            if (shouldShowUpdateNotification)
            {
                commandResult
                    .Should()
                    .HaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.WorkloadUpdatesAvailable);
            }
            else
            {
                commandResult
                    .Should()
                    .NotHaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.WorkloadUpdatesAvailable);
            }

        }

        [Fact]
        public void WorkloadUpdatesForDifferentBandAreNotAdvertised()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);
            var sdkFeatureBand = new SdkFeatureBand(TestContext.Current.ToolsetUnderTest.SdkVersion);
            // Write fake updates file
            Directory.CreateDirectory(Path.Combine(testInstance.Path, ".dotnet"));
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", $".workloadAdvertisingUpdates6.0.100"), @"[""maui""]");
            // Don't check for updates again and overwrite our existing updates file
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", ".workloadAdvertisingManifestSentinel" + sdkFeatureBand.ToString()), string.Empty);

            var command = new DotnetCommand(Log);
            var commandResult = command
                .WithEnvironmentVariable("DOTNET_CLI_HOME", testInstance.Path)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("build");

            commandResult
                .Should()
                .Pass();

            commandResult
                .Should()
                .NotHaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.WorkloadUpdatesAvailable);
          
            

        }

        [Fact]
        public void TestSideBySideUpdateChecks()
        {
            // this test checks that different version bands don't interfere with each other's update check timers
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            (var updater1, var downloader1, var sentinelPath1) = GetTestUpdater(testDir: testDir, featureBand: "6.0.100");
            (var updater2, var downloader2, var sentinelPath2) = GetTestUpdater(testDir: testDir, featureBand: "6.0.200");

            updater1.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            File.Exists(sentinelPath2).Should().BeFalse();

            downloader1.DownloadCallParams.Should().BeEquivalentTo(GetExpectedDownloadedPackages("6.0.100"));

            updater2.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            File.Exists(sentinelPath2).Should().BeTrue();
            downloader2.DownloadCallParams.Should().BeEquivalentTo(GetExpectedDownloadedPackages("6.0.200"));
            var updateTime2 = DateTime.Now;

            downloader1.DownloadCallParams.Clear();
            updater1.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            downloader1.DownloadCallParams.Should().BeEmpty();
            File.GetLastAccessTime(sentinelPath1).Should().BeBefore(updateTime2);

            downloader2.DownloadCallParams.Clear();
            updater2.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            // var updateTime1 = DateTime.Now;
            downloader2.DownloadCallParams.Should().BeEmpty();
            File.GetLastAccessTime(sentinelPath2).Should().BeCloseTo(updateTime2);
        }

       

        private List<(PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation)> GetExpectedDownloadedPackages(string sdkFeatureBand = "6.0.100")
        {
            var expectedDownloadedPackages = _installedManifests
                .Select(id => ((PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation))(new PackageId($"{id}.manifest-{sdkFeatureBand}"), null, null, null)).ToList();
            return expectedDownloadedPackages;
        }

        private (WorkloadManifestUpdater, MockNuGetPackageDownloader, string) GetTestUpdater([CallerMemberName] string testName = "", Func<string, string> getEnvironmentVariable = null)
        {
            var testDir = _testAssetsManager.CreateTestDirectory(testName: testName).Path;
            
            var featureBand = "6.0.100";

            return GetTestUpdater(testDir, featureBand, testName, getEnvironmentVariable);
        }

        private (WorkloadManifestUpdater, MockNuGetPackageDownloader, string) GetTestUpdater(string testDir, string featureBand, [CallerMemberName] string testName = "", Func<string, string> getEnvironmentVariable = null)
        {
            var dotnetRoot = Path.Combine(testDir, "dotnet");

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach (var manifest in _installedManifests)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("1.0.0")));
            }

            var manifestDirs = _installedManifests
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs)
            {
                SdkFeatureBand = new SdkFeatureBand(featureBand),
            };
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, dotnetRoot);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot, manifestDownload: true);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo, getEnvironmentVariable: getEnvironmentVariable);

            var sentinelPath = Path.Combine(testDir, _manifestSentinelFileName + featureBand);
            return (manifestUpdater, nugetDownloader, sentinelPath);
        }

        internal static string GetManifestContent(ManifestVersion version)
        {
            return $@"{{
  ""version"": {version.ToString().Substring(0, 1)},
  ""workloads"": {{
    }}
  }},
  ""packs"": {{
  }}
}}";
        }
    }
}
