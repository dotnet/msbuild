// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;

using System;
using System.IO;
using System.Linq;
using System.Text;

using Xunit;
using Xunit.Abstractions;

namespace ManifestReaderTests
{
    public class ManifestTests : SdkTest
    {
        private const string fakeRootPath = "fakeRootPath";
        private readonly string ManifestPath;
        private readonly string SampleProjectPath;

        public ManifestTests(ITestOutputHelper log) : base(log)
        {
            SampleProjectPath = _testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest");
            ManifestPath = GetSampleManifestPath("Sample.json");
        }

        string GetSampleManifestPath(string name) => Path.Combine(SampleProjectPath, name);

        [Fact]
        public void ItCanDeserialize()
        {
            using (FileStream fsSource = new FileStream(ManifestPath, FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest("Sample", fsSource);
                result.Version.Should().Be("5.0.0-preview1");
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
            buildToolsPack!.Id.ToString().Should().Be("Xamarin.Android.BuildTools");
            buildToolsPack.Version.Should().Be("8.4.7");
            buildToolsPack.Path.Should().Be(Path.Combine(fakeRootPath, "packs", "Xamarin.Android.BuildTools.Win64Host", "8.4.7"));
        }

        [Fact]
        public void UnresolvedAliasedPackPath()
        {
            var manifestProvider = new FakeManifestProvider(ManifestPath);
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { fakeRootPath }, new[] { "fake-platform" });

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "Xamarin.Android.BuildTools");

            buildToolsPack.Should().BeNull();
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

            var pack = resolver.TryGetPackInfo(new WorkloadPackId("Xamarin.Android.Sdk"));
            pack.Should().NotBeNull();

            string expectedPath = additionalExists ? additionalPackPath : defaultPackPath;

            pack!.Path.Should().Be(expectedPath);
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

            var pack = resolver.TryGetPackInfo(new WorkloadPackId("Xamarin.Android.Sdk"));
            pack.Should().NotBeNull();

            pack!.Path.Should().Be(defaultPackPath);
        }

        [Fact]
        public void ItChecksDependencies()
        {
            string MakeManifest(string version, params (string id, string version)[] dependsOn)
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendFormat("  \"version\": \"{0}\"", version);
                sb.AppendLine(dependsOn.Length > 0 ? "," : "");
                if (dependsOn.Length > 0)
                {
                    sb.AppendLine("  \"depends-on\": {");
                    for (int i = 0; i < dependsOn.Length; i++)
                    {
                        var dep = dependsOn[i];
                        sb.AppendFormat("    \"{0}\": \"{1}\"", dep.id, dep.version);
                        sb.AppendLine(i < dependsOn.Length - 1 ? "," : "");
                    }
                    sb.AppendLine("  }");
                }
                sb.AppendLine("}");
                return sb.ToString();
            }

            var goodManifestProvider = new InMemoryFakeManifestProvider
            {
                {  "AAA", MakeManifest("20.0.0", ("BBB", "5.0.0"), ("CCC", "63.0.0"), ("DDD", "25.0.0")) },
                {  "BBB", MakeManifest("8.0.0", ("DDD", "22.0.0")) },
                {  "CCC", MakeManifest("63.0.0") },
                {  "DDD", MakeManifest("25.0.0") },
            };

            WorkloadResolver.CreateForTests(goodManifestProvider, new[] { fakeRootPath });

            var missingManifestProvider = new InMemoryFakeManifestProvider
            {
                {  "AAA", MakeManifest("20.0.0", ("BBB", "5.0.0"), ("CCC", "63.0.0"), ("DDD", "25.0.0")) }
            };

            var missingManifestEx = Assert.Throws<WorkloadManifestCompositionException>(() => WorkloadResolver.CreateForTests(missingManifestProvider, new[] { fakeRootPath }));
            Assert.StartsWith("Did not find workload manifest dependency 'BBB' required by manifest 'AAA'", missingManifestEx.Message);

            var inconsistentManifestProvider = new InMemoryFakeManifestProvider
            {
                {  "AAA", MakeManifest("20.0.0", ("BBB", "5.0.0"), ("CCC", "63.0.0"), ("DDD", "25.0.0")) },
                {  "BBB", MakeManifest("8.0.0", ("DDD", "39.0.0")) },
                {  "CCC", MakeManifest("63.0.0") },
                {  "DDD", MakeManifest("30.0.0") },
            };

            var inconsistentManifestEx = Assert.Throws<WorkloadManifestCompositionException>(() => WorkloadResolver.CreateForTests(inconsistentManifestProvider, new[] { fakeRootPath }));
            Assert.StartsWith("Workload manifest dependency 'DDD' version '39.0.0' is lower than version '30.0.0' required by manifest 'BBB'", inconsistentManifestEx.Message);
        }

        [Fact]
        public void WillNotLoadManifestWithNullAlias()
        {
            using FileStream fsSource = new FileStream(GetSampleManifestPath("NullAliasError.json"), FileMode.Open, FileAccess.Read);

            var ex = Assert.Throws<WorkloadManifestFormatException> (() => WorkloadManifestReader.ReadWorkloadManifest("NullAliasError", fsSource));
            Assert.Contains("Expected string value at offset", ex.Message);
        }
    }
}
