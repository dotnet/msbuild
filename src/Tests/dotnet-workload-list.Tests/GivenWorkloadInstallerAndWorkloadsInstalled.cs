// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Update.Tests
{
    public class GivenInstalledWorkloadAndManifestUpdater : SdkTest
    {
        private const string CurrentSdkVersion = "6.0.101";
        private const string InstallingWorkload = "xamarin-android";
        private const string UpdateAvailableVersion = "7.0.100";
        private const string XamarinAndroidDescription = "xamarin-android description";
        private readonly BufferedReporter _reporter = new();
        private WorkloadListCommand _workloadListCommand;
        private string _testDirectory;

        private List<(ManifestVersionUpdate manifestUpdate, Dictionary<WorkloadId, WorkloadDefinition> Workloads)> _mockManifestUpdates;

        private MockNuGetPackageDownloader _nugetDownloader;
        private string _dotnetRoot;

        public GivenInstalledWorkloadAndManifestUpdater(ITestOutputHelper log) : base(log)
        {
        }

        private void Setup(string identifier)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(identifier: identifier).Path;
            _dotnetRoot = Path.Combine(_testDirectory, "dotnet");
            _nugetDownloader = new(_dotnetRoot);
            var currentSdkFeatureBand = new SdkFeatureBand(CurrentSdkVersion);

            _mockManifestUpdates = new()
            {
                (
                    new ManifestVersionUpdate(
                        new ManifestId("manifest1"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion(UpdateAvailableVersion),
                        currentSdkFeatureBand.ToString()),
                    new Dictionary<WorkloadId, WorkloadDefinition>
                    {
                        [new WorkloadId(InstallingWorkload)] = new(
                            new WorkloadId(InstallingWorkload), false, XamarinAndroidDescription,
                            WorkloadDefinitionKind.Dev, null, null, null),
                        [new WorkloadId("other")] = new(
                            new WorkloadId("other"), false, "other description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    }),
                (
                    new ManifestVersionUpdate(
                        new ManifestId("manifest-other"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion("7.0.101"),
                        currentSdkFeatureBand.ToString()),
                    new Dictionary<WorkloadId, WorkloadDefinition>
                    {
                        [new WorkloadId("other-manifest-workload")] = new(
                            new WorkloadId("other-manifest-workload"), false,
                            "other-manifest-workload description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    }),
                (
                    new ManifestVersionUpdate(
                        new ManifestId("manifest-older-version"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion("6.0.100"),
                        currentSdkFeatureBand.ToString()),
                    new Dictionary<WorkloadId, WorkloadDefinition>
                    {
                        [new WorkloadId("other-manifest-workload")] = new(
                            new WorkloadId("other-manifest-workload"), false,
                            "other-manifest-workload description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    })
            };

            ParseResult listParseResult = Parser.Instance.Parse(new[]
            {
                "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "7.0.100"
            });

            _workloadListCommand = new WorkloadListCommand(
                listParseResult,
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(_mockManifestUpdates),
                userProfileDir: _testDirectory,
                currentSdkVersion: CurrentSdkVersion,
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());
        }

        [Fact]
        public void ItShouldGetAvailableUpdate()
        {
            Setup(nameof(ItShouldGetAvailableUpdate));
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
            Setup(nameof(ItShouldGetListOfWorkloadWithCurrentSdkVersionBand));
            _workloadListCommand.Execute();
            _reporter.Lines.Should().Contain(c => c.Contains("\"installed\":[\"xamarin-android\"]"));
        }

        [Fact]
        public void GivenLowerTargetVersionItShouldThrow()
        {
            _workloadListCommand = new WorkloadListCommand(
                Parser.Instance.Parse(new[]
                {
                    "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "5.0.306"
                }),
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(_mockManifestUpdates),
                userProfileDir: _testDirectory,
                currentSdkVersion: CurrentSdkVersion,
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());

            Action a = () => _workloadListCommand.Execute();
            a.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GivenSameLowerTargetVersionBandItShouldNotThrow()
        {
            _workloadListCommand = new WorkloadListCommand(
                Parser.Instance.Parse(new[]
                {
                    "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "6.0.100"
                }),
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(_mockManifestUpdates),
                userProfileDir: _testDirectory,
                currentSdkVersion: "6.0.101",
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());

            Action a = () => _workloadListCommand.Execute();
            a.Should().NotThrow();
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
