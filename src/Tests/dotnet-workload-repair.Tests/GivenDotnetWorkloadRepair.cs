// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload;
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
            _parseResult = Parser.Instance.Parse("dotnet workload repair");
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenNoWorkloadsAreInstalledRepairIsNoOp(bool userLocal)
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var sdkFeatureVersion = "6.0.100";

            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot, userProfileDir: userProfileDir);
            repairCommand.Execute();

            _reporter.Lines.Should().Contain(LocalizableStrings.NoWorkloadsToRepair);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenExtraPacksInstalledRepairGarbageCollects(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var sdkFeatureVersion = "6.0.100";
            var installingWorkload = "xamarin-android";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            // Install a workload
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userProfileDir: userProfileDir, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();

            // Add extra pack dirs and records
            var extraPackRecordPath = Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1", "Test.Pack.A", "1.0.0", sdkFeatureVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(extraPackRecordPath));
            File.WriteAllText(extraPackRecordPath, string.Empty);
            var extraPackPath = Path.Combine(installRoot, "packs", "Test.Pack.A", "1.0.0");
            Directory.CreateDirectory(extraPackPath);

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver, userProfileDir: userProfileDir,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            repairCommand.Execute();

            // Check that pack dirs and records have been removed
            File.Exists(extraPackRecordPath).Should().BeFalse();
            Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(extraPackRecordPath))).Should().BeFalse();
            Directory.Exists(extraPackPath).Should().BeFalse();

            // Expected packs are still present
            Directory.GetDirectories(Path.Combine(installRoot, "packs")).Length.Should().Be(7);
            Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1")).Length.Should().Be(8);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenMissingPacksRepairFixesInstall(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var sdkFeatureVersion = "6.0.100";
            var installingWorkload = "xamarin-android";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            // Install a workload
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userProfileDir: userProfileDir, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();

            // Delete pack dirs/ records
            var deletedPackRecordPath = Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1", "Xamarin.Android.Sdk", "8.4.7", sdkFeatureVersion);
            File.Delete(deletedPackRecordPath);
            var deletedPackPath = Path.Combine(installRoot, "packs", "Xamarin.Android.Sdk");
            Directory.Delete(deletedPackPath, true);

            var repairCommand = new WorkloadRepairCommand(_parseResult, reporter: _reporter, workloadResolver: workloadResolver, userProfileDir: userProfileDir,
                nugetPackageDownloader: nugetDownloader, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            repairCommand.Execute();

            // Check that pack dirs and records have been replaced
            File.Exists(deletedPackRecordPath).Should().BeTrue();
            Directory.Exists(deletedPackPath).Should().BeTrue();

            // All expected packs are still present
            Directory.GetDirectories(Path.Combine(installRoot, "packs")).Length.Should().Be(7);
            Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1")).Length.Should().Be(8);
        }
    }
}
