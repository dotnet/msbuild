// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using Xunit;
using System.Linq;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace ManifestReaderTests
{
    public class ManifestReaderFunctionalTests : SdkTest
    {
        private readonly string ManifestPath;

        public ManifestReaderFunctionalTests(ITestOutputHelper log) : base(log)
        {
            ManifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [Fact]
        public void ItShouldGetAllTemplatesPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(1);
            var templateItem = result.First();
            templateItem.Id.ToString().Should().Be("Xamarin.Android.Templates");
            templateItem.IsStillPacked.Should().BeFalse();
            templateItem.Kind.Should().Be(WorkloadPackKind.Template);
            templateItem.Path.Should()
                .Be(Path.Combine("fakepath", "template-packs", "xamarin.android.templates.1.0.3.nupkg"));
        }

        [Fact]
        public void ItShouldGetAllSdkPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(5);
            var androidWorkloads = result.Single(w => w.Id == "Xamarin.Android.Sdk");
            androidWorkloads.Id.ToString().Should().Be("Xamarin.Android.Sdk");
            androidWorkloads.IsStillPacked.Should().BeTrue();
            androidWorkloads.Kind.Should().Be(WorkloadPackKind.Sdk);
            androidWorkloads.Version.Should().Be("8.4.7");
            androidWorkloads.Path.Should().Be(Path.Combine("fakepath", "packs", "Xamarin.Android.Sdk", "8.4.7"));
        }

        [Fact]
        public void ItShouldGetWorkloadDescription()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetWorkloadInfo(new WorkloadId("xamarin-android"));
            result.Description.Should().Be("Create, build and run Android apps");
        }

        private WorkloadResolver SetUp()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    new[] { "fakepath" });

            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);
            return workloadResolver;
        }

        [Fact]
        public void GivenTemplateNupkgDoesNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    new[] { "fakepath" });
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => false, directoryExists: (_) => true);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(0);
        }

        [Fact]
        public void GivenWorkloadSDKsDirectoryNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    new[] { "fakepath" });
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => false);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(0);
        }

        [Fact]
        public void ItCanReadIntegerVersion()
        {
            var testFolder = _testAssetsManager.CreateTestDirectory().Path;
            var manifestPath = Path.Combine(testFolder, "manifest.json");
            File.WriteAllText(manifestPath, @"
{
    ""version"": 5
}");

            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), new[] { "fakepath" });

            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);

            workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template).Should().BeEmpty();

        }
    }
}
