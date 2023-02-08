using System.CommandLine;
using System.IO;
using Xunit.Abstractions;
using Xunit;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using ManifestReaderTests;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload;
using FluentAssertions;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Repair;
using Microsoft.DotNet.Workloads.Workload.Clean;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.DotNet.Cli.Workload.List.Tests;
using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.Workload.Clean.Tests
{
    public class GivenDotnetWorkloadClean : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        private MockWorkloadManifestUpdater _manifestUpdater = new();
        private readonly string _sdkFeatureVersion = "6.0.100";
        private readonly string _installingWorkload = "xamarin-android";
        private readonly string dotnet = nameof(dotnet);
        private readonly string _profileDirectoryLeafName = "user-profile";

        public GivenDotnetWorkloadClean(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void GivenWorkloadCleanFileBasedItRemovesPacksAndPackRecords(bool userLocal, bool cleanAll)
        {
            // Setup ( I wish we could not copy this everywhere, but these are convenient as locals. )
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, dotnet);
            var userProfileDir = Path.Combine(testDirectory, _profileDirectoryLeafName);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, _sdkFeatureVersion);
            }

            // Test
            InstallWorkload(userProfileDir, dotnetRoot, testDirectory, workloadResolver, nugetDownloader);

            var extraPackRecordPath = MakePackRecord(installRoot);
            var extraPackPath = MakePack(installRoot);


            var cleanCommand = cleanAll ? GenerateWorkloadCleanAllCommand(workloadResolver, userProfileDir, dotnetRoot) : GenerateWorkloadCleanCommand(workloadResolver, userProfileDir, dotnetRoot);
            cleanCommand.Execute();

            AssertExtraneousPacksAreRemoved(extraPackPath, extraPackRecordPath);
            AssertValidPackCountsMatchExpected(installRoot, expectedPackCount: cleanAll ? 0 : 7, expectedPackRecordCount: cleanAll ? 0 : 8);

            AssertAdjacentCommandsStillPass(userProfileDir, dotnetRoot, testDirectory, workloadResolver, nugetDownloader);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadCleanAllFileBasedItCleansOnlyOlderAndCurrentFeatureBands(bool userLocal)
        {
            // Setup ( I wish we could not copy this everywhere, but these are convenient as locals. )
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, dotnet);
            var userProfileDir = Path.Combine(testDirectory, _profileDirectoryLeafName);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);

            const string aboveSdkFeatureBand = ToolsetInfo.NextTargetFrameworkVersion + ".100";
            const string belowSdkFeatureBand = "5.0.100"; // At the time of writing this test, it would only run on 7-8.0 SDKs or above.

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, _sdkFeatureVersion);
            }

            // Test
            InstallWorkload(userProfileDir, dotnetRoot, testDirectory, workloadResolver, nugetDownloader);

            var extraAbovePackRecordPath = MakePackRecord(installRoot, aboveSdkFeatureBand);
            var extraBelowPackRecordPath = MakePackRecord(installRoot, belowSdkFeatureBand);
            var extraPackPath = MakePack(installRoot);

            var cleanCommand = GenerateWorkloadCleanAllCommand(workloadResolver, userProfileDir, dotnetRoot);
            cleanCommand.Execute();

            AssertExtraneousPacksAreRemoved(extraPackPath, extraBelowPackRecordPath, true);
            AssertExtraneousPacksAreNotRemoved(extraPackPath, extraAbovePackRecordPath);
            AssertValidPackCountsMatchExpected(installRoot, expectedPackCount: 1, expectedPackRecordCount: 1);
        }

        private void InstallWorkload(string userProfileDir, string dotnetRoot, string testDirectory, WorkloadResolver workloadResolver, MockNuGetPackageDownloader nugetDownloader, string sdkBand = null)
        {
            sdkBand ??= _sdkFeatureVersion;

            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", _installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: _manifestUpdater, userProfileDir: userProfileDir, version: sdkBand, dotnetDir: dotnetRoot, tempDirPath: testDirectory, installedFeatureBand: sdkBand);

            installCommand.Execute();
        }

        private WorkloadCleanCommand GenerateWorkloadCleanCommand(WorkloadResolver workloadResolver, string userProfileDir, string dotnetRoot)
        {
            var cleanParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "clean" });
            return MakeWorkloadCleanCommand(cleanParseResult, workloadResolver, userProfileDir, dotnetRoot);
        }

        private WorkloadCleanCommand MakeWorkloadCleanCommand(ParseResult parseResult, WorkloadResolver workloadResolver, string userProfileDir, string dotnetRoot)
        {
            return new WorkloadCleanCommand(parseResult, reporter: _reporter, workloadResolver: workloadResolver, userProfileDir: userProfileDir,
                version: _sdkFeatureVersion, dotnetDir: dotnetRoot);
        }

        private WorkloadCleanCommand GenerateWorkloadCleanAllCommand(WorkloadResolver workloadResolver, string userProfileDir, string dotnetRoot)
        {
            var cleanParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "clean", "--all" });
            return MakeWorkloadCleanCommand(cleanParseResult, workloadResolver, userProfileDir, dotnetRoot);
        }

        private string MakePackRecord(string installRoot, string sdkBand = null)
        {
            sdkBand ??= _sdkFeatureVersion;

            var packRecordPath = Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1", "Test.Pack.A", "1.0.0", sdkBand);
            Directory.CreateDirectory(Path.GetDirectoryName(packRecordPath));
            File.WriteAllText(packRecordPath, string.Empty);
            return packRecordPath;
        }

        private string MakePack(string installRoot)
        {
            var packPath = Path.Combine(installRoot, "packs", "Test.Pack.A", "1.0.0");
            Directory.CreateDirectory(packPath);
            return packPath;
        }

        private void AssertExtraneousPacksAreRemoved(string extraPackPath, string extraPackRecordPath, bool entirePackRootPathShouldRemain = false)
        {
            File.Exists(extraPackRecordPath).Should().BeFalse();
            if (!entirePackRootPathShouldRemain)
            {
                Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(extraPackRecordPath))).Should().BeFalse();
                Directory.Exists(extraPackPath).Should().BeFalse();
            }
        }

        private void AssertExtraneousPacksAreNotRemoved(string extraPackPath, string extraPackRecordPath)
        {
            File.Exists(extraPackRecordPath).Should().BeTrue();
            Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(extraPackRecordPath))).Should().BeTrue();
            Directory.Exists(extraPackPath).Should().BeTrue();
        }

        private void AssertValidPackCountsMatchExpected(string installRoot, int expectedPackCount, int expectedPackRecordCount)
        {
            Directory.GetDirectories(Path.Combine(installRoot, "packs")).Length.Should().Be(expectedPackCount);
            Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1")).Length.Should().Be(expectedPackRecordCount);
        }

        /// <summary>
        /// Validate that commands that are likely to fail with invalid packs or invalid pack records do not fail, as an "end to end" safety precaution.
        /// </summary>
        private void AssertAdjacentCommandsStillPass(string userProfileDir, string dotnetRoot, string testDirectory, WorkloadResolver workloadResolver, MockNuGetPackageDownloader nugetDownloader, string sdkBand = null)
        {
            InstallWorkload(userProfileDir, dotnetRoot, testDirectory, workloadResolver, nugetDownloader, sdkBand);
        }
    }
}
