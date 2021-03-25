// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ManifestReaderTests
{
    public class ManifestTests : SdkTest
    {
        private const string fakeRootPath = "fakeRootPath";
        private readonly string ManifestPath;

        public ManifestTests(ITestOutputHelper log) : base(log)
        {
            ManifestPath = Path.Combine(_testAssetsManager.GetTestManifestsDirectory(), "SampleManifest", "Sample.json");
        }

        [Fact]
        public void ItCanDeserialize()
        {
            using (FileStream fsSource = new FileStream(ManifestPath, FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest(fsSource);
                result.Version.Should().Be(5);
                var xamAndroidId = new WorkloadPackId("Xamarin.Android.Sdk");

                result.Packs[xamAndroidId].Id.Should().Be(xamAndroidId);
                result.Packs[xamAndroidId].IsAlias.Should().Be(false);
                result.Packs[xamAndroidId].Kind.Should().Be(WorkloadPackKind.Sdk);
                result.Packs[xamAndroidId].Version.Should().Be("8.4.7");
            }
        }

        [Fact]
        public void AliasedPackPath()
        {
            var manifestProvider = new FakeManifestProvider(ManifestPath);
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { fakeRootPath });

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "Xamarin.Android.BuildTools");

            buildToolsPack.Should().NotBeNull();
            buildToolsPack.Id.Should().Be("Xamarin.Android.BuildTools");
            buildToolsPack.Version.Should().Be("8.4.7");
            buildToolsPack.Path.Should().Be(Path.Combine(fakeRootPath, "packs", "Xamarin.Android.BuildTools.Win64Host", "8.4.7"));
        }

        [Fact]
        public void GivenMultiplePackRoots_ItUsesTheLastOneIfThePackDoesntExist()
        {
            TestMultiplePackRoots(false, false);
        }

        [Fact]
        public void GivenMultiplePackRoots_ItUsesTheFirstOneIfBothExist()
        {
            TestMultiplePackRoots(true, true);
        }

        [Fact]
        public void GivenMultiplePackRoots_ItUsesTheFirstOneIfOnlyItExists()
        {
            TestMultiplePackRoots(false, true);
        }

        [Fact]
        public void GivenMultiplePackRoots_ItUsesTheSecondOneIfOnlyItExists()
        {
            TestMultiplePackRoots(true, false);
        }

        void TestMultiplePackRoots(bool defaultExists, bool additionalExists)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: defaultExists.ToString() + "_" + additionalExists.ToString()).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            Directory.CreateDirectory(dotnetRoot);
            var additionalRoot = Path.Combine(testDirectory, "additionalPackRoot");
            Directory.CreateDirectory(additionalRoot);

            var defaultPackPath = Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7");
            var additionalPackPath = Path.Combine(additionalRoot, "packs", "Xamarin.Android.Sdk", "8.4.7");

            if (defaultExists)
            {
                Directory.CreateDirectory(defaultPackPath);
            }
            if (additionalExists)
            {
                Directory.CreateDirectory(additionalPackPath);
            }

            var manifestProvider = new FakeManifestProvider(ManifestPath);
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { additionalRoot, dotnetRoot });

            var pack = resolver.TryGetPackInfo("Xamarin.Android.Sdk");
            pack.Should().NotBeNull();

            string expectedPath = additionalExists ? additionalPackPath : defaultPackPath;

            pack.Path.Should().Be(expectedPath);
        }

        [Fact]
        public void GivenNonExistentPackRoot_ItIgnoresIt()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            Directory.CreateDirectory(dotnetRoot);
            var additionalRoot = Path.Combine(testDirectory, "additionalPackRoot");

            var defaultPackPath = Path.Combine(dotnetRoot, "packs", "Xamarin.Android.Sdk", "8.4.7");
            Directory.CreateDirectory(defaultPackPath);

            var manifestProvider = new FakeManifestProvider(ManifestPath);
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { additionalRoot, dotnetRoot });

            var pack = resolver.TryGetPackInfo("Xamarin.Android.Sdk");
            pack.Should().NotBeNull();

            pack.Path.Should().Be(defaultPackPath);
        }
    }
}
