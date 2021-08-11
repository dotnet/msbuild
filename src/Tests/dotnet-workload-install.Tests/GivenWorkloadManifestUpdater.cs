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

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenWorkloadManifestUpdater : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestFileName = "WorkloadManifest.json";
        private readonly string _manifestSentinalFileName = ".workloadAdvertisingManifestSentinal";
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
            var expectedDownloadedPackages = _installedManifests
                .Select(id => ((PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation))(new PackageId($"{id}.manifest-6.0.100"), null, null, null));
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(expectedDownloadedPackages);
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItUpdatesWhenNoSentinalExists()
        {
            (var manifestUpdater, var nugetDownloader, var testDir) = GetTestUpdater();

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            var expectedDownloadedPackages = _installedManifests
                .Select(id => ((PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation))(new PackageId($"{id}.manifest-6.0.100"), null, null, null));
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(expectedDownloadedPackages);
            File.Exists(Path.Combine(testDir, ".dotnet", _manifestSentinalFileName)).Should().BeTrue();
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItUpdatesWhenDue()
        {
            Func<string, string> getEnvironmentVariable = (envVar) => envVar.Equals(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS) ? "0" : string.Empty;
            (var manifestUpdater, var nugetDownloader, var testDir) = GetTestUpdater(getEnvironmentVariable: getEnvironmentVariable);

            var sentinalPath = Path.Combine(testDir, ".dotnet", _manifestSentinalFileName);
            File.WriteAllText(sentinalPath, string.Empty);
            var createTime = DateTime.Now;

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();

            var expectedDownloadedPackages = _installedManifests
                .Select(id => ((PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation))(new PackageId($"{id}.manifest-6.0.100"), null, null, null));
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(expectedDownloadedPackages);
            File.Exists(sentinalPath).Should().BeTrue();
            File.GetLastAccessTime(sentinalPath).Should().BeAfter(createTime);
        }

        [Fact]
        public void GivenAdvertisingManifestUpdateItDoesNotUpdateWhenNotDue()
        {
            (var manifestUpdater, var nugetDownloader, var testDir) = GetTestUpdater();

            var sentinalPath = Path.Combine(testDir, ".dotnet", _manifestSentinalFileName);
            File.Create(sentinalPath);
            var createTime = DateTime.Now;

            manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync().Wait();
            nugetDownloader.DownloadCallParams.Should().BeEmpty();
            File.GetLastAccessTime(sentinalPath).Should().BeBefore(createTime);
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
            var expectedManifestUpdates = new (ManifestId, ManifestVersion, ManifestVersion)[] {
                (new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), new ManifestVersion("7.0.0")),
                (new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), new ManifestVersion("4.0.0")) };
            var expectedManifestNotUpdated = new ManifestId[] { new ManifestId("test-manifest-3"), new ManifestId("test-manifest-4") };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach ((var manifestId, var existingVersion, var newVersion) in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(existingVersion));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(newVersion));
            }
            foreach (var manifest in expectedManifestNotUpdated)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
            }

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.Item1)
                .Concat(expectedManifestNotUpdated)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, new string[] { dotnetRoot });
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            var manifestUpdates = manifestUpdater.CalculateManifestUpdates().Select( m => (m.manifestId, m.existingVersion,m .newVersion));
            manifestUpdates.Should().BeEquivalentTo(expectedManifestUpdates);
        }

        [Fact]
        public void GivenWorkloadManifestRollbackItCanCalculateUpdates()
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

            var rollbackDefContent = JsonSerializer.Serialize(new Dictionary<string, string>() { { "test-manifest-1", "4.0.0" }, { "test-manifest-2", "2.0.0" } });
            var rollbackDefPath = Path.Combine(testDir, "testRollbackDef.txt");
            File.WriteAllText(rollbackDefPath, rollbackDefContent);

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.Item1)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, new string[] { dotnetRoot });
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
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(Array.Empty<string>()), new string[] { dotnetRoot });
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            var exceptionThrown = Assert.Throws<Exception>(() => manifestUpdater.CalculateManifestRollbacks(rollbackDefPath));
            exceptionThrown.Message.Should().Contain(rollbackDefPath);
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
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(Array.Empty<string>()), new string[] { dotnetRoot });
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);

            var exceptionThrown = Assert.Throws<Exception>(() => manifestUpdater.CalculateManifestRollbacks(rollbackDefPath));
            exceptionThrown.Message.Should().Contain(rollbackDefPath);
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
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, new string[] { dotnetRoot });
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo);
            manifestUpdater.UpdateAdvertisingManifestsAsync(false, new DirectoryPath(offlineCache)).Wait();

            // We should have chosen the higher version manifest package to install/ extract
            nugetDownloader.ExtractCallParams.Count().Should().Be(1);
            nugetDownloader.ExtractCallParams[0].Item1.Should().Be(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.3.0.0.nupkg"));
        }

        [Theory]
        [InlineData("build")]
        [InlineData("publish")]
        public void GivenWorkloadsAreOutOfDateUpdatesAreAdvertisedOnRestoringCommands(string commandName)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: commandName)
                .WithSource()
                .Restore(Log);

            // Write fake updates file
            Directory.CreateDirectory(Path.Combine(testInstance.Path, ".dotnet"));
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", ".workloadAdvertisingUpdates"), @"[""maui""]");
            // Don't check for updates again and overwrite our existing updates file
            File.WriteAllText(Path.Combine(testInstance.Path, ".dotnet", ".workloadAdvertisingManifestSentinal"), string.Empty);

            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_CLI_HOME", testInstance.Path)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(commandName)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.WorkloadUpdatesAvailable);
        }

        private (WorkloadManifestUpdater, MockNuGetPackageDownloader, string) GetTestUpdater([CallerMemberName] string testName = "", Func<string, string> getEnvironmentVariable = null)
        {
            var testDir = _testAssetsManager.CreateTestDirectory(testName: testName).Path;
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var featureBand = "6.0.100";

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
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var workloadResolver = WorkloadResolver.CreateForTests(workloadManifestProvider, new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot, manifestDownload: true);
            var installationRepo = new MockInstallationRecordRepository();
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadResolver, nugetDownloader, testDir, testDir, installationRepo, getEnvironmentVariable: getEnvironmentVariable);

            return (manifestUpdater, nugetDownloader, testDir);
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
