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
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;

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
        public void GivenManagedInstallItCanGetFeatureBandsWhenFilesArePresent()
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

            var featureBands = installer.GetWorkloadInstallationRecordRepository().GetFeatureBandsWithInstallationRecords();
            featureBands.ShouldBeEquivalentTo(versions);
        }

        [Fact]
        public void GivenManagedInstallItCanNotGetFeatureBandsWhenFilesAreNotPresent()
        {
            var versions = new string[] { "6.0.100", "6.0.300", "7.0.100" };
            var (dotnetRoot, installer, _) = GetTestInstaller();

            // Write fake workloads
            foreach (var version in versions)
            {
                var path = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads");
                Directory.CreateDirectory(path);
            }

            var featureBands = installer.GetWorkloadInstallationRecordRepository().GetFeatureBandsWithInstallationRecords();
            featureBands.Should().BeEmpty();
        }

        [Fact]
        public void GivenManagedInstallItCanGetInstalledWorkloads()
        {
            var version = "6.0.100";
            var workloads = new string[] { "test-workload-1", "test-workload-2", "test-workload3" };
            var (dotnetRoot, installer, _) = GetTestInstaller();

            // Write fake workloads
            var net6Path = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads");
            Directory.CreateDirectory(net6Path);
            foreach (var workload in workloads)
            {
                File.WriteAllText(Path.Combine(net6Path, workload), string.Empty);
            }
            var net7Path = Path.Combine(dotnetRoot, "metadata", "workloads", "7.0.100", "InstalledWorkloads");
            Directory.CreateDirectory(net7Path);
            File.WriteAllText(Path.Combine(net7Path, workloads.First()), string.Empty);

            var installedWorkloads = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(version));
            installedWorkloads.ShouldBeEquivalentTo(workloads);
        }

        [Fact]
        public void GivenManagedInstallItCanWriteInstallationRecord()
        {
            var workloadId = new WorkloadId("test-workload");
            var version = "6.0.100";
            var (dotnetRoot, installer, _) = GetTestInstaller();
            installer.GetWorkloadInstallationRecordRepository().WriteWorkloadInstallationRecord(workloadId, new SdkFeatureBand(version));
            var expectedPath = Path.Combine(dotnetRoot, "metadata", "workloads", version, "InstalledWorkloads", workloadId.ToString());
            File.Exists(expectedPath).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallDirectoryPacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            var mockNugetInstaller = nugetInstaller as MockNuGetPackageDownloader;
            mockNugetInstaller.DownloadCallParams.Count.Should().Be(1);
            mockNugetInstaller.DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version), null as DirectoryPath?, null as PackageSourceLocation));
            mockNugetInstaller.ExtractCallParams.Count.Should().Be(1);
            mockNugetInstaller.ExtractCallParams[0].Item1.Should().Be(mockNugetInstaller.DownloadCallResult[0]);
            mockNugetInstaller.ExtractCallParams[0].Item2.ToString().Should().Contain($"{packInfo.Id}-{packInfo.Version}-extracted");

            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();

            Directory.Exists(packInfo.Path).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallSingleFilePacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Template, Path.Combine(dotnetRoot, "template-packs", "Xamarin.Android.Sdk.8.4.7.nupkg"), "Xamarin.Android.Sdk");
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            (nugetInstaller as MockNuGetPackageDownloader).DownloadCallParams.Count.Should().Be(1);
            (nugetInstaller as MockNuGetPackageDownloader).DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version), null as DirectoryPath?, null as PackageSourceLocation));
            (nugetInstaller as MockNuGetPackageDownloader).ExtractCallParams.Count.Should().Be(0);

            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();

            File.Exists(packInfo.Path).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallPacksWithAliases()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var alias = "Xamarin.Android.BuildTools.Alias";
            var packInfo = new PackInfo("Xamarin.Android.BuildTools", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", alias, "8.4.7"), alias);
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            (nugetInstaller as MockNuGetPackageDownloader).DownloadCallParams.Count.Should().Be(1);
            (nugetInstaller as MockNuGetPackageDownloader).DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId(alias), new NuGetVersion(packInfo.Version), null as DirectoryPath?, null as PackageSourceLocation));
        }

        [Fact]
        public void GivenManagedInstallItHonorsNuGetSources()
        {
            var packageSource = new PackageSourceLocation(new FilePath("mock-file"));
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller(packageSourceLocation: packageSource);
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var version = "6.0.100";
            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            var mockNugetInstaller = nugetInstaller as MockNuGetPackageDownloader;
            mockNugetInstaller.DownloadCallParams.Count.Should().Be(1);
            mockNugetInstaller.DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version), null as DirectoryPath?, packageSource));
        }

        [Fact]
        public void GivenManagedInstallItDetectsInstalledPacks()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var version = "6.0.100";

            // Mock installing the pack
            Directory.CreateDirectory(packInfo.Path);

            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version));

            (nugetInstaller as MockNuGetPackageDownloader).DownloadCallParams.Count.Should().Be(0);
        }

        [Fact]
        public void GivenManagedInstallItCanRollBackInstallFailures()
        {
            var version = "6.0.100";
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller(failingInstaller: true);
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            
            var exceptionThrown = Assert.Throws<Exception>(() => installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version)));
            exceptionThrown.Message.Should().Be("Test Failure");
            var failingNugetInstaller = nugetInstaller as FailingNuGetPackageDownloader;
            // Nupkgs should be removed
            Directory.GetFiles(failingNugetInstaller.MockPackageDir).Should().BeEmpty();
            // Packs should be removed
            Directory.Exists(packInfo.Path).Should().BeFalse();
        }

        [Fact]
        public void GivenManagedInstallItCanGarbageCollect()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Library, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk"),
                new PackInfo("Xamarin.Android.Framework", "8.4.0", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.4.0"), "Xamarin.Android.Framework")
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
        public void GivenManagedInstallItCanGarbageCollectPacksMissingFromManifest()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            // Define packs that don't show up in the manifest
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk.fake", "8.4.7", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk.fake", "8.4.7"), "Xamarin.Android.Sdk.fake"),
                new PackInfo("Xamarin.Android.Framework.mock", "8.4", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework.mock", "8.4"), "Xamarin.Android.Framework.mock")
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
                    File.WriteAllText(packRecordPath, JsonSerializer.Serialize(pack));
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

        [Fact]
        public void GivenManagedInstallItDoesNotRemovePacksWithInstallRecords()
        {
            var (dotnetRoot, installer, _) = GetTestInstaller();
            var packs = new PackInfo[]
            {
                new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Library, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk"),
                new PackInfo("Xamarin.Android.Framework", "8.4.0", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Framework", "8.4.0"), "Xamarin.Android.Framework")
            };
            var packsToBeGarbageCollected = new PackInfo[]
            {
                new PackInfo("Test.Pack.A", "1.0.0", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Test.Pack.A", "1.0.0"), "Test.Pack.A"),
                new PackInfo("Test.Pack.B", "2.0.0", WorkloadPackKind.Framework, Path.Combine(dotnetRoot, "packs", "Test.Pack.B", "2.0.0"), "Test.Pack.B"),
            };
            var sdkVersions = new string[] { "6.0.100", "6.0.300" };

            // Write fake packs
            var installedPacksPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1");
            foreach (var sdkVersion in sdkVersions)
            {
                Directory.CreateDirectory(Path.Combine(dotnetRoot, "metadata", "workloads", sdkVersion, "InstalledWorkloads"));
                foreach (var pack in packs.Concat(packsToBeGarbageCollected))
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

            foreach (var pack in packsToBeGarbageCollected)
            {
                Directory.Exists(pack.Path)
                    .Should()
                    .BeFalse();
            }
        }

        [Fact]
        public void GivenManagedInstallItCanInstallManifestVersion()
        {
            var (_, installer, nugetDownloader) = GetTestInstaller(manifestDownload: true);
            var featureBand = new SdkFeatureBand("6.0.100");
            var manifestId = new ManifestId("test-manifest-1");
            var manifestVersion = new ManifestVersion("5.0.0");

            installer.InstallWorkloadManifest(manifestId, manifestVersion, featureBand);

            var mockNugetInstaller = nugetDownloader as MockNuGetPackageDownloader;
            mockNugetInstaller.DownloadCallParams.Count.Should().Be(1);
            mockNugetInstaller.DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId($"{manifestId}.manifest-{featureBand}"),
                new NuGetVersion(manifestVersion.ToString()), null as DirectoryPath?, null as PackageSourceLocation));
        }
		
	    [Fact]
        public void GivenManagedInstallItCanDownloadToOfflineCache()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var version = "6.0.100";
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var cachePath = Path.Combine(dotnetRoot, "MockCache");
            installer.DownloadToOfflineCache(packInfo, new DirectoryPath(cachePath), false);

            var mockNugetInstaller = nugetInstaller as MockNuGetPackageDownloader;
            mockNugetInstaller.DownloadCallParams.Count.Should().Be(1);
            mockNugetInstaller.DownloadCallParams[0].ShouldBeEquivalentTo((new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version), new DirectoryPath(cachePath) as DirectoryPath?, null as PackageSourceLocation));
            Directory.Exists(cachePath).Should().BeTrue();

            // Should have only been downloaded, not installed
            mockNugetInstaller.ExtractCallParams.Count.Should().Be(0);
            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeFalse();
            Directory.Exists(packInfo.Path).Should().BeFalse();
        }

        [Fact]
        public void GivenManagedInstallItCanInstallPacksFromOfflineCache()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var version = "6.0.100";
            var cachePath = Path.Combine(dotnetRoot, "MockCache");

            // Write mock cache
            Directory.CreateDirectory(cachePath);
            var nupkgPath = Path.Combine(cachePath, $"{packInfo.ResolvedPackageId}.{packInfo.Version}.nupkg");
            File.Create(nupkgPath);

            installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version), new DirectoryPath(cachePath));
            var mockNugetInstaller = nugetInstaller as MockNuGetPackageDownloader;
            
            // We shouldn't download anything, use the cache
            mockNugetInstaller.DownloadCallParams.Count.Should().Be(0);

            // Otherwise install should be normal
            mockNugetInstaller.ExtractCallParams.Count.Should().Be(1);
            mockNugetInstaller.ExtractCallParams[0].Item1.Should().Be(nupkgPath);
            mockNugetInstaller.ExtractCallParams[0].Item2.ToString().Should().Contain($"{packInfo.Id}-{packInfo.Version}-extracted");
            var installationRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1", packInfo.Id, packInfo.Version, version);
            File.Exists(installationRecordPath).Should().BeTrue();
            Directory.Exists(packInfo.Path).Should().BeTrue();
        }

        [Fact]
        public void GivenManagedInstallItCanErrorsWhenMissingOfflineCache()
        {
            var (dotnetRoot, installer, nugetInstaller) = GetTestInstaller();
            var packInfo = new PackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk, Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7"), "Xamarin.Android.Sdk");
            var version = "6.0.100";
            var cachePath = Path.Combine(dotnetRoot, "MockCache");
            
            var exceptionThrown = Assert.Throws<Exception>(() => installer.InstallWorkloadPack(packInfo, new SdkFeatureBand(version), new DirectoryPath(cachePath)));
            exceptionThrown.Message.Should().Contain(packInfo.ResolvedPackageId);
            exceptionThrown.Message.Should().Contain(packInfo.Version);
            exceptionThrown.Message.Should().Contain(cachePath);
        }

        private (string, NetSdkManagedInstaller, INuGetPackageDownloader) GetTestInstaller([CallerMemberName] string testName = "", bool failingInstaller = false, string identifier = "", bool manifestDownload = false,
            PackageSourceLocation packageSourceLocation = null)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier: identifier).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            INuGetPackageDownloader nugetInstaller = failingInstaller ? new FailingNuGetPackageDownloader(testDirectory) :  new MockNuGetPackageDownloader(dotnetRoot, manifestDownload);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var sdkFeatureBand = new SdkFeatureBand("6.0.100");
            return (dotnetRoot, new NetSdkManagedInstaller(_reporter, sdkFeatureBand, workloadResolver, nugetInstaller, dotnetRoot, packageSourceLocation: packageSourceLocation, tempDirPath: testDirectory), nugetInstaller);
        }
    }
}
