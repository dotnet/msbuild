// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Workload.Update.Tests
{
    public class GivenInstalledWorkloadAndManifestUpdater : SdkTest
    {
        private const string CurrentSdkVersion = "6.0.101";
        private const string InstallingWorkload = "xamarin-android";
        private const string UpdateAvailableVersion = "7.0.100";
        private const string XamarinAndroidDescription = "xamarin-android description";
        private readonly BufferedReporter _reporter = new();
        private readonly WorkloadListCommand _workloadListCommand;

        public GivenInstalledWorkloadAndManifestUpdater(ITestOutputHelper log) : base(log)
        {
            string testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            string dotnetRoot = Path.Combine(testDirectory, "dotnet");
            MockNuGetPackageDownloader nugetDownloader = new(dotnetRoot);

            List<(ManifestId, ManifestVersion, ManifestVersion, Dictionary<WorkloadDefinitionId, WorkloadDefinition>
                Workloads)> mockManifestUpdates =
                new List<(ManifestId, ManifestVersion, ManifestVersion,
                    Dictionary<WorkloadDefinitionId, WorkloadDefinition> Workloads)>
                {
                    (
                        new ManifestId("manifest1"),
                        new ManifestVersion(CurrentSdkVersion),
                        new ManifestVersion(UpdateAvailableVersion),
                        new Dictionary<WorkloadDefinitionId, WorkloadDefinition>
                        {
                            [new WorkloadDefinitionId(InstallingWorkload)] = new(
                                new WorkloadDefinitionId(InstallingWorkload), false, XamarinAndroidDescription,
                                WorkloadDefinitionKind.Dev, null, null, null),
                            [new WorkloadDefinitionId("other")] = new(
                                new WorkloadDefinitionId("other"), false, "other description",
                                WorkloadDefinitionKind.Dev, null, null, null)
                        }),
                    (
                        new ManifestId("manifest-other"),
                        new ManifestVersion(CurrentSdkVersion),
                        new ManifestVersion("7.0.101"),
                        new Dictionary<WorkloadDefinitionId, WorkloadDefinition>
                        {
                            [new WorkloadDefinitionId("other-manifest-workload")] = new(
                                new WorkloadDefinitionId("other-manifest-workload"), false,
                                "other-manifest-workload description",
                                WorkloadDefinitionKind.Dev, null, null, null)
                        })
                };

            // Update workload
            ParseResult listParseResult = Parser.GetWorkloadsInstance.Parse(new[]
            {
                "dotnet", "workload", "list", "--machine-readable", "--target-sdk-version", "7.0.100"
            });

            _workloadListCommand = new WorkloadListCommand(
                listParseResult,
                _reporter,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(mockManifestUpdates),
                userHome: testDirectory,
                currentSdkVersion: CurrentSdkVersion,
                dotnetDir: dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());
        }

        [Fact]
        public void ItShouldGetAvailableUpdate()
        {
            WorkloadListCommand.UpdateAvailableEntry[] result =
                _workloadListCommand.GetUpdateAvailable(new List<WorkloadId> {new("xamarin-android")});

            result.Should().NotBeEmpty();
            result[0].WorkloadId.Should().Be(InstallingWorkload, "Only should installed workload");
            result[0].ExistingManifestVersion.Should().Be(CurrentSdkVersion);
            result[0].AvailableUpdateManifestVersion.Should().Be(UpdateAvailableVersion);
            result[0].Description.Should().Be(XamarinAndroidDescription);
        }

        [Fact]
        public void ItShouldGetListOfWorkloadWithCurrentSdkVersionBand()
        {
            _workloadListCommand.Execute();
            _reporter.Lines.Should().Contain(c => c.Contains("\"installed\":[\"xamarin-android\"]"));
        }

        internal class MockMatchingFeatureBandInstallationRecordRepository : IWorkloadInstallationRecordRepository
        {
            public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) =>
                throw new NotImplementedException();

            public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) =>
                throw new NotImplementedException();

            public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
            {
                SdkFeatureBand featureBand = new SdkFeatureBand(new ReleaseVersion(6, 0, 100));
                if (sdkFeatureBand.Equals(featureBand))
                {
                    return new[] {new WorkloadId("xamarin-android")};
                }

                throw new Exception($"Should not pass other feature band {sdkFeatureBand}");
            }

            public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() =>
                throw new NotImplementedException();
        }
    }
}
