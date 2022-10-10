// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Cli.Workload.Search.Tests;
using Microsoft.DotNet.PackageInstall.Tests;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Common;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace dotnet.Tests
{
    public class TempDirectoryWorkloadManifestProviderTests : SdkTest
    {
        private string _manifestDirectory;
        private string _testDirectory;
        private string _updaterDir;

        public TempDirectoryWorkloadManifestProviderTests(ITestOutputHelper logger) : base(logger)
        {
        }

        private void Initialize(string testName)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(testName).Path;
            _manifestDirectory = Path.Combine(_testDirectory, "temp-extract");
            _updaterDir = Path.Combine(_testDirectory, "temp-dir");
            Directory.CreateDirectory(_manifestDirectory);
            Directory.CreateDirectory(_updaterDir);
        }

        [WindowsOnlyFact]
        public void ItShouldReturnListOfManifestFiles()
        {
            Initialize(nameof(ItShouldReturnListOfManifestFiles));
            NuGetPackageDownloader nuGetPackageDownloader = new NuGetPackageDownloader(new DirectoryPath(_updaterDir),
                null,
                new MockFirstPartyNuGetPackageSigningVerifier(),
                new NullLogger(), restoreActionConfig: new RestoreActionConfig(NoCache: true));

            MockWorkloadResolver mockWorkloadResolver = new(Enumerable.Empty<WorkloadResolver.WorkloadInfo>());
            WorkloadManifestUpdater workloadManifestUpdater =
                new WorkloadManifestUpdater(new BufferedReporter(),
                    mockWorkloadResolver, nuGetPackageDownloader,
                    _updaterDir, _updaterDir, new MockInstallationRecordRepository());

            string package = DownloadSamplePackage(new PackageId("Microsoft.NET.Workload.Emscripten.Manifest-6.0.100"),
                NuGetVersion.Parse("6.0.0-preview.7.21377.2"), nuGetPackageDownloader);

            workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(new List<string> {package},
                new DirectoryPath(_manifestDirectory)).GetAwaiter().GetResult();

            TempDirectoryWorkloadManifestProvider tempDirectoryWorkloadManifestProvider =
                new TempDirectoryWorkloadManifestProvider(_manifestDirectory, mockWorkloadResolver.GetSdkFeatureBand());
            IEnumerable<ReadableWorkloadManifest> manifest =
                tempDirectoryWorkloadManifestProvider.GetManifests();
            manifest.First().ManifestId.Should()
                .NotBe("microsoft.net.workload.emscripten.manifest-6.0.100.6.0.0-preview.7.21377.2");
            manifest.First().ManifestId.Should()
                .BeEquivalentTo("microsoft.net.workload.emscripten");
        }

        private string DownloadSamplePackage(PackageId packageId, NuGetVersion version,
            NuGetPackageDownloader nuGetPackageDownloader)
        {
            return ExponentialRetry.ExecuteWithRetry(
                    DownloadMostRecentSamplePackageFromPublicFeed,
                    result => result != null,
                    3,
                    () => ExponentialRetry.Timer(ExponentialRetry.Intervals),
                    "Run command while retry transient restore error")
                .ConfigureAwait(false).GetAwaiter().GetResult();

            string DownloadMostRecentSamplePackageFromPublicFeed()
            {
                try
                {
                    return nuGetPackageDownloader.DownloadPackageAsync(
                            packageId, version, includePreview: true,
                            packageSourceLocation: new PackageSourceLocation(
                                sourceFeedOverrides: new[] {"https://api.nuget.org/v3/index.json"})).GetAwaiter()
                        .GetResult();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
