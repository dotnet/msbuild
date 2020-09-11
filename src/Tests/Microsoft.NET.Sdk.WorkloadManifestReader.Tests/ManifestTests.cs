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

                result.Packs["xamarin.android.sdk"].Id.Should().Be("xamarin.android.sdk");
                result.Packs["xamarin.android.sdk"].IsAlias.Should().Be(false);
                result.Packs["xamarin.android.sdk"].Kind.Should().Be(WorkloadPackKind.Sdk);
                result.Packs["xamarin.android.sdk"].Version.Should().Be("8.4.7");
            }
        }

        [Fact]
        public void AliasedPackPath()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = new WorkloadResolver(manifestProvider, fakeRootPath);

            resolver.ReplaceFilesystemChecksForTest(_ => true, _ => true);
            resolver.ReplacePlatformIdsForTest(new[] { "win-x64", "*" });

            var buildToolsPack = resolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk).FirstOrDefault(pack => pack.Id == "xamarin.android.buildtools");

            buildToolsPack.Should().NotBeNull();
            buildToolsPack.Id.Should().Be("xamarin.android.buildtools");
            buildToolsPack.Version.Should().Be("8.4.7");
            buildToolsPack.Path.Should().Be(Path.Combine(fakeRootPath, "packs", "xamarin.android.buildtools.win64host", "8.4.7"));
        }
    }
}
