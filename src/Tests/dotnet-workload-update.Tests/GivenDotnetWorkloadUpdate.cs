// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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

            var exceptionThrown = Assert.Throws<Exception>(() => command.Execute());
            exceptionThrown.Message.Should().Be("Failing workload: xamarin-android-build");
            var expectedPacks = mockWorkloadIds
                .SelectMany(workloadId => workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                .Distinct()
                .Select(packId => workloadResolver.TryGetPackInfo(packId));
            installer.RolledBackPacks.ShouldBeEquivalentTo(expectedPacks);
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEmpty();
        }

        private (string, WorkloadUpdateCommand, MockPackWorkloadInstaller, IWorkloadResolver, MockWorkloadManifestUpdater, MockNuGetPackageDownloader) GetTestInstallers(
            ParseResult parseResult,
            [CallerMemberName] string testName = "",
            string failingWorkload = null,
            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestUpdates = null,
            IList<WorkloadId> installedWorkloads = null)
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory(testName: testName).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller(failingWorkload, installedWorkloads: installedWorkloads);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater(manifestUpdates);
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
