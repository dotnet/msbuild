// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using System.Linq;
using Xunit;

namespace ManifestReaderTests
{
    public class ManifestTests
    {
        private const string fakeRootPath = "fakeRootPath";

        [Fact]
        public void ItCanDeserialize()
        {
            using (FileStream fsSource = new FileStream(Path.Combine("Manifests", "Sample.json"), FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest(fsSource);
                result.Version.Should().Be(5);

                result.Packs["Xamarin.Android.Sdk"].Id.Should().Be("Xamarin.Android.Sdk");
                result.Packs["Xamarin.Android.Sdk"].IsAlias.Should().Be(false);
                result.Packs["Xamarin.Android.Sdk"].Kind.Should().Be(WorkloadPackKind.Sdk);
                result.Packs["Xamarin.Android.Sdk"].Version.Should().Be("8.4.7");
            }
        }

        [Fact]
        public void AliasedPackPath()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = new WorkloadResolver(manifestProvider, fakeRootPath);

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);
            resolver.ReplacePlatformIdsForTest(new[] { "win-x64", "*" });

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "Xamarin.Android.BuildTools");

            buildToolsPack.Should().NotBeNull();
            buildToolsPack.Id.Should().Be("Xamarin.Android.BuildTools");
            buildToolsPack.Version.Should().Be("8.4.7");
            buildToolsPack.Path.Should().Be(Path.Combine(fakeRootPath, "packs", "Xamarin.Android.BuildTools.Win64Host", "8.4.7"));
        }
    }
}
