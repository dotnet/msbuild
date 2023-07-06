// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Workload.Uninstall.Tests
{
    public class GivenDotnetWorkloadUninstall : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        private string SetUpMockWorkloadToUninstall(string fakeWorkloadNameToInstall, string sdkFeatureVersion, string testDirectory, bool userLocal)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            InstallWorkload(fakeWorkloadNameToInstall, testDirectory, sdkFeatureVersion);

            // Assert install was successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", fakeWorkloadNameToInstall))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);

            return installRoot;

        }

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallWorkload(bool userLocal)
        {
            var installingWorkload = "mock-1";
            var sdkFeatureVersion = "6.0.100";
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;

            var installRoot = SetUpMockWorkloadToUninstall(installingWorkload, sdkFeatureVersion, testDirectory, userLocal);

            UninstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(0);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeFalse();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(0);
        }

        [Fact]
        public void GivenWorkloadUninstallItWorksWithVerbosityFlag()
        {
            bool userLocal = true; // The locality doesnt really matter as we just want to make sure the flag(s) are supported.
            var installingWorkload = "mock-1";
            var sdkFeatureVersion = "6.0.100";
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;

            SetUpMockWorkloadToUninstall(installingWorkload, sdkFeatureVersion, testDirectory, userLocal);

            string[] args = { "--verbosity", "diag" };
            var exitCode = UninstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion, args);
            exitCode.Should().Be(0, "The exit code of workload uninstall should be 0 to indicate success when the flag was added.");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedWorkload(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var sdkFeatureVersion = "6.0.100";
            var installedWorkload = "mock-1";
            var uninstallingWorkload = "mock-2";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            InstallWorkload(installedWorkload, testDirectory, sdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(3);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(4);

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedFeatureBand(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var prevSdkFeatureVersion = "5.0.100";
            var sdkFeatureVersion = "6.0.100";
            var uninstallingWorkload = "mock-1";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, prevSdkFeatureVersion);
            }

            InstallWorkload(uninstallingWorkload, testDirectory, prevSdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
            var featureBandMarkerFiles = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"))
                .SelectMany(packIdDirs => Directory.GetDirectories(packIdDirs))
                .SelectMany(packVersionDirs => Directory.GetFiles(packVersionDirs));
            featureBandMarkerFiles.Count().Should().Be(6); // 3 packs x 2 feature bands

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        private void InstallWorkload(string installingWorkload, string testDirectory, string sdkFeatureVersion)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(dotnetRoot, sdkFeatureVersion);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userProfileDir: userProfileDir, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory, installedFeatureBand: sdkFeatureVersion);
            installCommand.Execute();
        }

        private int UninstallWorkload(string uninstallingWorkload, string testDirectory, string sdkFeatureVersion, string[] args = null)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(dotnetRoot, sdkFeatureVersion);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);

            var command = new List<string> { "dotnet", "workload", "uninstall", uninstallingWorkload};
            if(args != null)
            {
                command.AddRange(args);
            }

            var uninstallParseResult = Parser.Instance.Parse(command);
            var uninstallCommand = new WorkloadUninstallCommand(uninstallParseResult, reporter: _reporter, workloadResolver, nugetDownloader,
                dotnetDir: dotnetRoot, version: sdkFeatureVersion, userProfileDir: userProfileDir);
            return uninstallCommand.Execute();
        }
    }
}
