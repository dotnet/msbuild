// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
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
        private readonly string _manifestPath;

        public GivenNetSdkManagedWorkloadInstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [Fact]
        public void GivenManagedInstallItsInsallationUnitIsPacks()
        {
            var (_, installer, _) = GetTestInstaller();
            installer.GetInstallationUnit().Should().Be(InstallationUnit.Packs);
        }

        [Fact]
        public void GivenManagedInstallItCanGetFeatureBands()
        {
            var versions = new string[] { "6.0.100", "6.0.300", "7.0.100" };
            var (dotnetRoot, installer, _) = GetTestInstaller();

            // Write fake workloads
            foreach (var version in versions)
            {
                var path = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads");
                Directory.CreateDirectory(path);
                File.Create(Path.Combine(path, "6.0.100"));
            }

            var featureBands = installer.GetFeatureBandsWithInstallationRecords();
            featureBands.ShouldBeEquivalentTo(versions);
        }

        [Fact]
        public void GivenManagedInstallItCanGetInstalledWorkloads()
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
        public void GivenManagedInstallItCanWriteInstallationRecord()
        {
            var workloadId = new WorkloadId("test-workload");
            var version = "6.0.100";
            var (dotnetRoot, installer, _) = GetTestInstaller();
            installer.WriteWorkloadInstallationRecord(workloadId, new SdkFeatureBand(version));
            var expectedPath = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads", workloadId.ToString());
            File.Exists(expectedPath).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallDirectoryPacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"));
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            var mockNugetInstaller = nugetInstaller as MockNuGetPackageDownloader;
            mockNugetInstaller.InstallCallParams.Count.Should().Be(1);
            mockNugetInstaller.InstallCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)));
            mockNugetInstaller.ExtractCallParams.Count.Should().Be(1);
            mockNugetInstaller.ExtractCallParams[0].ShouldBeEquivalentTo((mockNugetInstaller.InstallCallResult[0], Path.Combine(dotnetRoot, "metadata", "temp", $"{packInfo.Id}-{packInfo.Version}-extracted")));

            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();

            Directory.Exists(packInfo.Path).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallSingleFilePacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Template, Path.Combine(dotnetRoot, "template-packs", "Xamarin.Android.Sdk.8.4.7.nupkg"));
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            (nugetInstaller as MockNuGetPackageDownloader).InstallCallParams.Count.Should().Be(1);
            (nugetInstaller as MockNuGetPackageDownloader).InstallCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)));
            (nugetInstaller as MockNuGetPackageDownloader).ExtractCallParams.Count.Should().Be(0);

            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();

            File.Exists(packInfo.Path).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItDetectInstalledPacks()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"));
            var version = "6.0.100";

            // Mock installing the pack
            Directory.CreateDirectory(packInfo.Path);

            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            _reporter.Lines.Should().Contain(string.Format(Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings.WorkloadPackAlreadyInstalledMessage, packInfo.Id, packInfo.Version));
        }

        [Fact]
        public void GivenManagedInstallItCanRollBackInstallFailures()
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
                var failingNugetInstaller = nugetInstaller as FailingNuGetPackageDownloader;

                e.Message.Should().Be("Test Failure");
                // Nupkgs should be removed
                Directory.GetFiles(failingNugetInstaller.MockPackageDir).Should().BeEmpty();
                // Packs should be removed
                Directory.Exists(packInfo.Path).Should().BeFalse();
            }
        }

        [Fact]
        public void GivenManagedInstallItCanGarbageCollect()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Library, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7")),
                new PackInfo("Xamarin.Android.Framework", "8.4", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.4"))
            };
            var sdkVersions = new string[] { "6.0.100", "6.0.300" };

            // Write fake packs
            var installedPacksPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1");
            foreach (var sdkVersion in sdkVersions)
            {
                foreach (var pack in packs)
                {
                    var packRecordPath = Path.Combine(installedPacksPath, pack.Id, pack.Version, sdkVersion);
                    Directory.CreateDirectory(Path.GetDirectoryName(packRecordPath));
                    File.WriteAllText(packRecordPath, string.Empty);
                    Directory.CreateDirectory(pack.Path);
                }
            }
            // Write fake install record for 6.0.100
            var workloadsRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", sdkVersions.First(), "InstalledWorkloads");
            Directory.CreateDirectory(workloadsRecordPath);
            File.Create(Path.Combine(workloadsRecordPath, "xamarin-empty-mock"));

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

        [Fact]
        public void GivenManagedInstallItDoesNotRemovePacksWithInstallRecords()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Library, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7")),
                new PackInfo("Xamarin.Android.Framework", "8.4", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.4"))
            };
            var sdkVersions = new string[] { "6.0.100", "6.0.300" };

            // Write fake packs
            var installedPacksPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1");
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
            // Write fake workload install record for 6.0.100
            var installedWorkloadsPath = Path.Combine(dotnetRoot, "metadata", "workloads", sdkVersions.First(), "InstalledWorkloads", "xamarin-android-build");
            File.WriteAllText(installedWorkloadsPath, string.Empty);

            installer.GarbageCollectInstalledWorkloadPacks();

            Directory.EnumerateFileSystemEntries(installedPacksPath)
                .Should()
                .NotBeEmpty();
            foreach (var pack in packs)
            {
                Directory.Exists(pack.Path)
                    .Should()
                    .BeTrue();

                var expectedRecordPath = Path.Combine(installedPacksPath, pack.Id, pack.Version, sdkVersions.First());
                File.Exists(expectedRecordPath)
                    .Should()
                    .BeTrue();
            }
        }

        private (string, NetSdkManagedInstaller, INuGetPackageDownloader) GetTestInstaller([CallerMemberName] string identifier = "", bool failingInstaller = false)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            INuGetPackageDownloader nugetInstaller = failingInstaller ? new FailingNuGetPackageDownloader(testDirectory) :  new MockNuGetPackageDownloader(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var sdkFeatureBand = new SdkFeatureBand("6.0.100");
            return (dotnetRoot, new NetSdkManagedInstaller(_reporter, sdkFeatureBand, workloadResolver, nugetInstaller, dotnetRoot), nugetInstaller);
        }
    }
}
