// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Uninstall.Tests
{
    public class GivenDotnetWorkloadUninstall : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadUninstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
        }

        [Fact]
        public void GivenWorkloadUninstallItErrorsWhenWorkloadIsNotInstalled()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var exceptionThrown = Assert.Throws<GracefulException>(() => UninstallWorkload("mock-1", testDirectory, "6.0.100"));
            exceptionThrown.Message.Should().Contain("mock-1");
        }

        [Fact]
        public void GivenWorkloadUninstallItCanUninstallWorkload()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var sdkFeatureVersion = "6.0.100";
            var installingWorkload = "mock-1";

            InstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion);

            // Assert install was successful
            var installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);

            UninstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful
            installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(0);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeFalse();
            packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(0);
        }

        [Fact]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedWorkload()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var sdkFeatureVersion = "6.0.100";
            var installedWorkload = "mock-1";
            var uninstallingWorkload = "mock-2";

            InstallWorkload(installedWorkload, testDirectory, sdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(3);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(4);

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        [Fact]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedFeatureBand()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var prevSdkFeatureVersion = "5.0.100";
            var sdkFeatureVersion = "6.0.100";
            var uninstallingWorkload = "mock-1";

            InstallWorkload(uninstallingWorkload, testDirectory, prevSdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
            var featureBandMarkerFiles = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"))
                .SelectMany(packIdDirs => Directory.GetDirectories(packIdDirs))
                .SelectMany(packVersionDirs => Directory.GetFiles(packVersionDirs));
            featureBandMarkerFiles.Count().Should().Be(6); // 3 packs x 2 feature bands

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(dotnetRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        private void InstallWorkload(string installingWorkload, string testDirectory, string sdkFeatureVersion)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var installParseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userHome: testDirectory, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();
        }

        private void UninstallWorkload(string uninstallingWorkload, string testDirectory, string sdkFeatureVersion)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var uninstallParseResult = Parser.GetWorkloadsInstance.Parse(new string[] { "dotnet", "workload", "uninstall", uninstallingWorkload });
            var uninstallCommand = new WorkloadUninstallCommand(uninstallParseResult, reporter: _reporter, workloadResolver, nugetDownloader,
                dotnetDir: dotnetRoot, version: sdkFeatureVersion);
            uninstallCommand.Execute();
        }
    }
}
