// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.NET.Sdk.WorkloadManifestReader;

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
            using (FileStream fsSource = new(ManifestPath, FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest("Sample", fsSource, ManifestPath);
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
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath);

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
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath, currentRuntimeIdentifiers: new[] { "fake-platform" });

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "Xamarin.Android.BuildTools");

            buildToolsPack.Should().BeNull();
        }

        [Fact]
        public void GivenMultiplePackRoots_ItUsesTheFirstInstallableIfThePackDoesntExist()
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
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { (additionalRoot, false), (dotnetRoot, true), ("other", true) });

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
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, new[] { (additionalRoot, false), (dotnetRoot, true) });

            var pack = resolver.TryGetPackInfo(new WorkloadPackId("Xamarin.Android.Sdk"));
            pack.Should().NotBeNull();

            pack!.Path.Should().Be(defaultPackPath);
        }

        [Fact]
        public void ItChecksDependencies()
        {
            static string MakeManifest(string version, params (string id, string version)[] dependsOn)
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

            WorkloadResolver.CreateForTests(goodManifestProvider, fakeRootPath);

            var missingManifestProvider = new InMemoryFakeManifestProvider
            {
                {  "AAA", MakeManifest("20.0.0", ("BBB", "5.0.0"), ("CCC", "63.0.0"), ("DDD", "25.0.0")) }
            };

            var missingManifestEx = Assert.Throws<WorkloadManifestCompositionException>(() => WorkloadResolver.CreateForTests(missingManifestProvider, fakeRootPath));
            Assert.StartsWith("Did not find workload manifest dependency 'BBB' required by manifest 'AAA'", missingManifestEx.Message);

            var inconsistentManifestProvider = new InMemoryFakeManifestProvider
            {
                {  "AAA", MakeManifest("20.0.0", ("BBB", "5.0.0"), ("CCC", "63.0.0"), ("DDD", "25.0.0")) },
                {  "BBB", MakeManifest("8.0.0", ("DDD", "39.0.0")) },
                {  "CCC", MakeManifest("63.0.0") },
                {  "DDD", MakeManifest("30.0.0") },
            };

            var inconsistentManifestEx = Assert.Throws<WorkloadManifestCompositionException>(() => WorkloadResolver.CreateForTests(inconsistentManifestProvider, fakeRootPath));
            Assert.StartsWith("Workload manifest dependency 'DDD' version '30.0.0' is lower than version '39.0.0' required by manifest 'BBB'", inconsistentManifestEx.Message);
        }

        [Fact]
        public void WillNotLoadManifestWithNullAlias()
        {
            var manifestPath = GetSampleManifestPath("NullAliasError.json");
            using FileStream fsSource = new(manifestPath, FileMode.Open, FileAccess.Read);

            var ex = Assert.Throws<WorkloadManifestFormatException>(() => WorkloadManifestReader.ReadWorkloadManifest("NullAliasError", fsSource, manifestPath));
            Assert.Contains("Expected string value at offset", ex.Message);
        }

        [Fact]
        public void ItCanFindLocalizationCatalog()
        {
            string expected = MakePathNative("manifests/My.Manifest/localize/WorkloadManifest.pt-BR.json");

            string? locPath = WorkloadManifestReader.GetLocalizationCatalogFilePath(
                    "manifests/My.Manifest/WorkloadManifest.json",
                    CultureInfo.GetCultureInfo("pt-BR"),
                    s => true
                );

            Assert.Equal(expected, locPath);
        }

        [Fact]
        public void ItCanFindParentCultureLocalizationCatalog()
        {
            string expected = MakePathNative("manifests/My.Manifest/localize/WorkloadManifest.pt.json");

            string? locPath = WorkloadManifestReader.GetLocalizationCatalogFilePath(
                    "manifests/My.Manifest/WorkloadManifest.json",
                    CultureInfo.GetCultureInfo("pt-BR"),
                    s => s == expected
                );

            Assert.Equal(expected, locPath);
        }

        static string MakePathNative(string path) => path.Replace('/', Path.DirectorySeparatorChar);

        [Fact]
        public void ItCanLocalizeDescriptions()
        {
            var manifest = GetSampleManifestPath("Sample.json");
            var locCatalog = GetSampleManifestPath("Sample.loc.json");

            var provider = new FakeManifestProvider((manifest, locCatalog));

            var resolver = WorkloadResolver.CreateForTests(provider, fakeRootPath);

            var workloads = resolver.GetAvailableWorkloads().ToList();

            var xamAndroid = workloads.FirstOrDefault(w => w.Id == "xamarin-android");
            Assert.Equal("Localized description for xamarin-android", xamAndroid?.Description);

            var xamAndroidBuild = workloads.FirstOrDefault(w => w.Id == "xamarin-android-build");
            Assert.Equal("Localized description for xamarin-android-build", xamAndroidBuild?.Description);
        }
    }
}
