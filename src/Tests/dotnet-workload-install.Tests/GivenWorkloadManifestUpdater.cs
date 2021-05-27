// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenWorkloadManifestUpdater : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestFileName = "WorkloadManifest.json";

        public GivenWorkloadManifestUpdater(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItCanUpdateAdvertisingManifests()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var installedManifests = new ManifestId[] { new ManifestId("test-manifest-1"), new ManifestId("test-manifest-2"), new ManifestId("test-manifest-3") };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach (var manifest in installedManifests)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("1.0.0")));
            }

            var manifestDirs = installedManifests
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot, manifestDownload: true);
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadManifestProvider, nugetDownloader, testDir, testDir);

            manifestUpdater.UpdateAdvertisingManifestsAsync(true).Wait();
            var expectedDownloadedPackages = installedManifests
                .Select(id => ((PackageId, NuGetVersion, DirectoryPath?, PackageSourceLocation))(new PackageId($"{id}.manifest-{featureBand}"), null, null, null));
            nugetDownloader.DownloadCallParams.Should().BeEquivalentTo(expectedDownloadedPackages);
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItCanCalculateUpdates()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");
            var expectedManifestUpdates = new (ManifestId, ManifestVersion, ManifestVersion)[] {
                (new ManifestId("test-manifest-1"), new ManifestVersion("5.0.0"), new ManifestVersion("7.0.0")),
                (new ManifestId("test-manifest-2"), new ManifestVersion("3.0.0"), new ManifestVersion("4.0.0")) };
            var expectedManifestNotUpdated = new ManifestId[] { new ManifestId("test-manifest-3"), new ManifestId("test-manifest-4") };

            // Write mock manifests
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(installedManifestDir);
            Directory.CreateDirectory(adManifestDir);
            foreach ((var manifestId, var existingVersion, var newVersion) in expectedManifestUpdates)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(existingVersion));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifestId.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifestId.ToString(), _manifestFileName), GetManifestContent(newVersion));
            }
            foreach (var manifest in expectedManifestNotUpdated)
            {
                Directory.CreateDirectory(Path.Combine(installedManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
                Directory.CreateDirectory(Path.Combine(adManifestDir, manifest.ToString()));
                File.WriteAllText(Path.Combine(adManifestDir, manifest.ToString(), _manifestFileName), GetManifestContent(new ManifestVersion("5.0.0")));
            }

            var manifestDirs = expectedManifestUpdates.Select(manifest => manifest.Item1)
                .Concat(expectedManifestNotUpdated)
                .Select(manifest => Path.Combine(installedManifestDir, manifest.ToString(), _manifestFileName))
                .ToArray();
            var workloadManifestProvider = new MockManifestProvider(manifestDirs);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadManifestProvider, nugetDownloader, testDir, testDir);

            var manifestUpdates = manifestUpdater.CalculateManifestUpdates();
            manifestUpdates.Should().BeEquivalentTo(expectedManifestUpdates);
        }

        [Fact]
        public void GivenWorkloadManifestUpdateItChoosesHighestManifestVersionInCache()
        {
            var manifestId = "mock-manifest";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var featureBand = "6.0.100";
            var dotnetRoot = Path.Combine(testDir, "dotnet");

            // Write mock manifest
            var installedManifestDir = Path.Combine(testDir, "dotnet", "sdk-manifests", featureBand);
            var adManifestDir = Path.Combine(testDir, ".dotnet", "sdk-advertising", featureBand);
            Directory.CreateDirectory(adManifestDir);
            Directory.CreateDirectory(Path.Combine(installedManifestDir, manifestId));
            File.WriteAllText(Path.Combine(installedManifestDir, manifestId, _manifestFileName), GetManifestContent(new ManifestVersion("1.0.0")));

            // Write multiple manifest packages to the offline cache
            var offlineCache = Path.Combine(testDir, "cache");
            Directory.CreateDirectory(offlineCache);
            File.Create(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.2.0.0.nupkg"));
            File.Create(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.3.0.0.nupkg"));

            var workloadManifestProvider = new MockManifestProvider(new string[] { Path.Combine(installedManifestDir, manifestId, _manifestFileName) });
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new WorkloadManifestUpdater(_reporter, workloadManifestProvider, nugetDownloader, testDir, testDir);
            manifestUpdater.UpdateAdvertisingManifestsAsync(false, new DirectoryPath(offlineCache)).Wait();

            // We should have chosen the higher version manifest package to install/ extract
            nugetDownloader.ExtractCallParams.Count().Should().Be(1);
            nugetDownloader.ExtractCallParams[0].Item1.Should().Be(Path.Combine(offlineCache, $"{manifestId}.manifest-{featureBand}.3.0.0.nupkg"));
        }

        internal static string GetManifestContent(ManifestVersion version)
        {
            return $@"{{
  ""version"": {version.ToString().Substring(0, 1)},
  ""workloads"": {{
    }}
  }},
  ""packs"": {{
  }}
}}";
        }
    }
}
