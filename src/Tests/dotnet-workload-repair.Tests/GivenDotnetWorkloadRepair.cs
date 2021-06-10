// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload.Repair;
using System.IO;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload.Install;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Repair.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Workload.Repair.Tests
{
    public class GivenDotnetWorkloadRepair : SdkTest
    {
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadRepair(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _parseResult = Parser.GetWorkloadsInstance.Parse("dotnet workload repair");
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledRepairIsNoOp()
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var sdkFeatureVersion = "6.0.100";

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot);
            repairCommand.Execute();

            _reporter.Lines.Should().Contain(string.Format(LocalizableStrings.RepairSucceeded, string.Empty));
        }

        [Fact]
        public void GivenExtraPacksInstalledRepairGarbageCollects()
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
                workloadManifestUpdater: manifestUpdater, userHome: testDirectory, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();

            // Add extra pack dirs and records
            var extraPackRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", "Test.Pack.A", "1.0.0", sdkFeatureVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(extraPackRecordPath));
            File.WriteAllText(extraPackRecordPath, string.Empty);
            var extraPackPath = Path.Combine(dotnetRoot, "packs", "Test.Pack.A", "1.0.0");
            Directory.CreateDirectory(extraPackPath);

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            repairCommand.Execute();

            // Check that pack dirs and records have been removed
            File.Exists(extraPackRecordPath).Should().BeFalse();
            Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(extraPackRecordPath))).Should().BeFalse();
            Directory.Exists(extraPackPath).Should().BeFalse();

            // Expected packs are still present
            Directory.GetDirectories(Path.Combine(dotnetRoot, "packs")).Length.Should().Be(7);
            Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1")).Length.Should().Be(8);
        }

        [Fact]
        public void GivenMissingPacksRepairFixesInstall()
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
                workloadManifestUpdater: manifestUpdater, userHome: testDirectory, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();

            // Delete pack dirs/ records
            var deletedPackRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", "Xamarin.Android.Sdk", "8.4.7", sdkFeatureVersion);
            File.Delete(deletedPackRecordPath);
            var deletedPackPath = Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk");
            Directory.Delete(deletedPackPath, true);

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            repairCommand.Execute();

            // Check that pack dirs and records have been replaced
            File.Exists(deletedPackRecordPath).Should().BeTrue();
            Directory.Exists(deletedPackPath).Should().BeTrue();

            // All expected packs are still present
            Directory.GetDirectories(Path.Combine(dotnetRoot, "packs")).Length.Should().Be(7);
            Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1")).Length.Should().Be(8);
        }
    }
}
