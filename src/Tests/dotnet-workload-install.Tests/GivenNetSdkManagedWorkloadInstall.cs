// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.NuGetPackageInstaller;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenNetSdkManagedWorkloadInstall : SdkTest
    {
        private readonly BufferedReporter _reporter;

        public GivenNetSdkManagedWorkloadInstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void ManagedInstallerInsallationUnitIsPacks()
        {
            var (_, installer, _) = GetTestInstaller();
            installer.GetInstallationUnit().Should().Be(InstallationUnit.Packs);
        }

        [Fact]
        public void ManagedInstallerCanGetFeatureBands()
        {
            var versions = new string[] { "6.0.100", "6.0.300", "7.0.100" };
            var (dotnetRoot, installer, _) = GetTestInstaller();

            // Write fake workloads
            foreach (var version in versions)
            {
                Directory.CreateDirectory(Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads"));
            }

            var featureBands = installer.GetFeatureBandsWithInstallationRecords();
            featureBands.ShouldBeEquivalentTo(versions);
        }

        [Fact]
        public void ManagedInstallerCanGetInstalledWorkloads()
        {
            var version = "6.0.100";
            var workloads = new string[] { "test-workload-1", "test-workload-2", "test-workload3" };
            var (dotnetRoot, installer, _) = GetTestInstaller();

            // Write fake workloads
            var path = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads");
            Directory.CreateDirectory(path);
            foreach (var workload in workloads)
            {
                File.WriteAllText(Path.Combine(path, workload), string.Empty);
            }

            var installedWorkloads = installer.GetInstalledWorkloads(new SdkFeatureBand(version));
            installedWorkloads.ShouldBeEquivalentTo(workloads);
        }

        [Fact]
        public void ManagedInstallerCanWriteInstallationRecord()
        {
            var workloadId = new WorkloadId("test-workload");
            var version = "6.0.100";
            var (dotnetRoot, installer, _) = GetTestInstaller();
            installer.WriteWorkloadInstallationRecord(workloadId, new SdkFeatureBand(version));
            var expectedPath = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads", workloadId.ToString());
            File.Exists(expectedPath).Should().BeTrue();
        }

        [Fact]
        public void ManagedInstallerCanInstallPacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"));
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            (nugetInstaller as MockNuGetPackageInstaller).InstallCallParams.Count.Should().Be(1);
            (nugetInstaller as MockNuGetPackageInstaller).InstallCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)));
            (nugetInstaller as MockNuGetPackageInstaller).ExtractCallParams.Count.Should().Be(1);
            (nugetInstaller as MockNuGetPackageInstaller).ExtractCallParams[0].ShouldBeEquivalentTo(("Mock/path", Path.Combine(dotnetRoot, "packs", packInfo.Id, packInfo.Version)));

            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();
        }

        [Fact]
        public void ManagedInstallerCanRollBackInstallFailures()
        {
            var version = "6.0.100";
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller(failingInstaller: true);
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"));
            try
            {
                installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

                // Install should have failed
                true.Should().BeFalse();
            }
            catch (Exception e)
            {
                var failingNugetInstaller = nugetInstaller as FailingNuGetPackageInstaller;

                e.Message.Should().Be("Test Failure");
                // Nupkgs should be removed
                Directory.Exists(failingNugetInstaller.MockPackageDir).Should().BeFalse();
                // Packs should be removed
                Directory.EnumerateFileSystemEntries(Path.Combine(dotnetRoot, "packs")).Should().BeEmpty();
            }
        }

        [Fact]
        public void ManagedInstallerGarbageCollect()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Library, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7")),
                new PackInfo("Xamarin.Android.Framework", "8.4", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.4"))
            };
            var sdkVersions = new string[] { "6.0.100", "6.0.300" };

            // Write fake packs
            var installedPacksPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks");
            foreach (var sdkVersion in sdkVersions)
            {
                Directory.CreateDirectory(Path.Combine(dotnetRoot, "metadata", "workloads", sdkVersion, "InstalledWorkloads"));
                foreach (var pack in packs)
                {
                    var packRecordPath = Path.Combine(installedPacksPath, pack.Id, pack.Version, sdkVersion);
                    Directory.CreateDirectory(Path.GetDirectoryName(packRecordPath));
                    File.WriteAllText(packRecordPath, string.Empty);
                    Directory.CreateDirectory(pack.Path);
                }
            }

            installer.GarbageCollectInstalledWorkloadPacks();

            Directory.EnumerateFileSystemEntries(installedPacksPath)
                .Should()
                .BeEmpty();
            foreach (var pack in packs)
            {
                Directory.Exists(pack.Path)
                    .Should()
                    .BeFalse();
            }
        }

        private (string, NetSdkManagedInstaller, INuGetPackageInstaller) GetTestInstaller([CallerMemberName] string identifier = "", bool failingInstaller = false)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier).Path;
            var manifestDir = Path.Combine(_testAssetsManager.GetTestManifestsDirectory(), "SampleManifest", "Sample.json");
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            INuGetPackageInstaller nugetInstaller = failingInstaller ? new FailingNuGetPackageInstaller(testDirectory) :  new MockNuGetPackageInstaller();
            return (dotnetRoot, new MockManagedInstaller(_reporter, nugetInstaller, dotnetRoot, manifestDir), nugetInstaller);
        }
    }
}
