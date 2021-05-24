// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload.Update;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Update.Tests
{
    public class GivenDotnetWorkloadUpdate : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;
        private readonly ParseResult _parseResult;

        public GivenDotnetWorkloadUpdate(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            _parseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "update" });
        }

        [Fact]
        public void GivenWorkloadUpdateItRemovesOldPacksAfterInstall()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var sdkFeatureVersion = "6.0.100";
            var installingWorkload = "xamarin-android";

            // Install a workload
            var installParseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userHome: testDirectory, version: sdkFeatureVersion, dotnetDir: dotnetRoot);
            installCommand.Execute();

            // 7 packs in packs dir, 1 template pack
            var installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(7);
            foreach (var packDir in installPacks)
            {
                Directory.GetDirectories(packDir).Count().Should().Be(1); // 1 version of each pack installed
            }
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", "Xamarin.Android.Sdk", "8.4.7", "6.0.100")) // Original pack version is installed
                .Should().BeTrue();
            File.Exists(Path.Combine(dotnetRoot, "template-packs", "xamarin.android.templates.1.0.3.nupkg"))
                .Should().BeTrue();
            // Install records are correct
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(8);
            foreach (var packRecordDir in packRecordDirs)
            {
                var packVersionRecordDirs = Directory.GetDirectories(packRecordDir);
                packVersionRecordDirs.Count().Should().Be(1); // 1 version of each pack installed
                Directory.GetFiles(packVersionRecordDirs.First()).Count().Should().Be(1); // 1 feature band file for this pack id and version
            }

            // Mock updating the manifest
            workloadResolver = WorkloadResolver.CreateForTests(
                new MockManifestProvider(new[] { Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleUpdatedManifest"), "Sample.json") }),
                new string[] { dotnetRoot });

            // Update workload
            var updateParseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "update" });
            var updateCommand = new WorkloadUpdateCommand(updateParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
            workloadManifestUpdater: manifestUpdater, userHome: testDirectory, version: sdkFeatureVersion, dotnetDir: dotnetRoot);
            updateCommand.Execute();

            // 6 packs in packs dir, 1 template pack
            var updatePacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            updatePacks.Count().Should().Be(6);
            foreach (var packDir in updatePacks)
            {
                Directory.GetDirectories(packDir).Count().Should().Be(1); // 1 version of each pack installed
            }
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", "Xamarin.Android.Sdk", "8.5.7", "6.0.100")) // New pack version is installed
                .Should().BeTrue();
            File.Exists(Path.Combine(dotnetRoot, "template-packs", "xamarin.android.templates.2.1.3.nupkg"))
                .Should().BeTrue();
            // Install records are correct
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(7);
            foreach (var packRecordDir in packRecordDirs)
            {
                var packVersionRecordDirs = Directory.GetDirectories(packRecordDir);
                packVersionRecordDirs.Count().Should().Be(1); // 1 version of each pack installed
                Directory.GetFiles(packVersionRecordDirs.First()).Count().Should().Be(1); // 1 feature band file for this pack id and version
            }
        }

        [Fact]
        public void GivenWorkloadUpdateItUpdatesOutOfDatePacks()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            (_, var command, var installer, _, _, _) = GetTestInstallers(_parseResult, installedWorkloads: mockWorkloadIds);

            command.Execute();

            installer.GarbageCollectionCalled.Should().BeTrue();
            installer.CachePath.Should().BeNull();
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEquivalentTo(mockWorkloadIds);
            installer.InstalledPacks.Count.Should().Be(8);
            installer.InstalledPacks.Where(pack => pack.Id.Contains("Android")).Count().Should().Be(8);
        }

        [Fact]
        public void GivenWorkloadUpdateItRollsBackOnFailedUpdate()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android"), new WorkloadId("xamarin-android-build") };
            (_, var command, var installer, var workloadResolver, _, _) = GetTestInstallers(_parseResult, installedWorkloads: mockWorkloadIds, failingWorkload: "xamarin-android-build");

            var exceptionThrown = Assert.Throws<GracefulException>(() => command.Execute());
            exceptionThrown.Message.Should().Contain("Failing workload: xamarin-android-build");
            var expectedPacks = mockWorkloadIds
                .SelectMany(workloadId => workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                .Distinct()
                .Select(packId => workloadResolver.TryGetPackInfo(packId));
            installer.RolledBackPacks.ShouldBeEquivalentTo(expectedPacks);
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEmpty();
        }

        [Fact]
        public void GivenWorkloadUpdateItCanDownloadToOfflineCache()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            var cachePath = Path.Combine(_testAssetsManager.CreateTestDirectory(identifier: "cachePath").Path, "mockCachePath");
            var parseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "update", "--download-to-cache", cachePath });
            (_, var command, var installer, _, var manifestUpdater, _) = GetTestInstallers(parseResult, installedWorkloads: mockWorkloadIds, includeInstalledPacks: true);

            command.Execute();

            // Manifest packages should have been 'downloaded' and used for pack resolution
            manifestUpdater.DownloadManifestPackagesCallCount.Should().Be(1);
            manifestUpdater.ExtractManifestPackagesToTempDirCallCount.Should().Be(1);
            // 6 android pack packages need to be updated
            installer.CachedPacks.Count.Should().Be(6);
            installer.CachedPacks.Select(pack => pack.Id).Should().NotContain("Xamarin.Android.Sdk"); // This pack is up to date, doesn't need to be cached
            installer.CachePath.Should().Be(cachePath);
        }

        [Fact]
        public void GivenWorkloadUpdateItCanInstallFromOfflineCache()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            var cachePath = "mockCachePath";
            var parseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "update", "--from-cache", cachePath });
            (_, var command, var installer, _, _, var nugetDownloader) = GetTestInstallers(parseResult, installedWorkloads: mockWorkloadIds);

            command.Execute();

            installer.GarbageCollectionCalled.Should().BeTrue();
            installer.CachePath.Should().Contain(cachePath);
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEquivalentTo(mockWorkloadIds);
            installer.InstalledPacks.Count.Should().Be(8);
            installer.InstalledPacks.Where(pack => pack.Id.Contains("Android")).Count().Should().Be(8);
            nugetDownloader.DownloadCallParams.Count().Should().Be(0);
        }

        [Fact]
        public void GivenWorkloadUpdateItPrintsDownloadUrls()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            var parseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "update", "--print-download-link-only" });
            (_, var command, _, _, _, _) = GetTestInstallers(parseResult, installedWorkloads: mockWorkloadIds, includeInstalledPacks: true);

            command.Execute();

            _reporter.Lines.Should().Contain("==allPackageLinksJsonOutputStart==");
            string.Join(" ", _reporter.Lines).Should().Contain("mock-url-xamarin.android.templates", "New pack urls should be included in output");
            string.Join(" ", _reporter.Lines).Should().Contain("mock-url-xamarin.android.framework", "Urls for packs with updated versions should be included in output");
            string.Join(" ", _reporter.Lines).Should().NotContain("mock-url-xamarin.android.sdk", "Urls for packs with the same version should not be included in output");
            string.Join(" ", _reporter.Lines).Should().Contain("mock-manifest-url");
        }

        private (string, WorkloadUpdateCommand, MockPackWorkloadInstaller, IWorkloadResolver, MockWorkloadManifestUpdater, MockNuGetPackageDownloader) GetTestInstallers(
            ParseResult parseResult,
            [CallerMemberName] string testName = "",
            string failingWorkload = null,
            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestUpdates = null,
            IList<WorkloadId> installedWorkloads = null,
            bool includeInstalledPacks = false)
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory(testName: testName).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installedPacks = new PackInfo[] {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk"),
                new PackInfo("Xamarin.Android.Framework", "8.2.0", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.2.0"), "Xamarin.Android.Framework")
            };
            var installer = includeInstalledPacks ?
                new MockPackWorkloadInstaller(failingWorkload, installedWorkloads: installedWorkloads, installedPacks: installedPacks) :
                new MockPackWorkloadInstaller(failingWorkload, installedWorkloads: installedWorkloads);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater(manifestUpdates, _manifestPath);
            var installManager = new WorkloadUpdateCommand(
                parseResult,
                reporter: _reporter,
                workloadResolver: workloadResolver,
                workloadInstaller: installer,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater,
                userHome: testDirectory,
                version: "6.0.100");

            return (testDirectory, installManager, installer, workloadResolver, manifestUpdater, nugetDownloader);
        }
    }
}
