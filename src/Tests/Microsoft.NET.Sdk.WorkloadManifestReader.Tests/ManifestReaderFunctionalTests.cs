// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using Xunit;
using System.Linq;

namespace ManifestReaderTests
{
    public class ManifestReaderFunctionalTests
    {
        [Fact]
        public void ItShouldGetAllTemplatesPacks()
        {
            var workloadResolver = new WorkloadResolver(new FakeManifestProvider(new[] { Path.Combine("Manifests", "Sample.json") }), "fakepath");
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(1);
            var templateItem = result.First();
            templateItem.Id.Should().Be("xamarin.android.templates");
            templateItem.IsStillPacked.Should().BeFalse();
            templateItem.Kind.Should().Be(WorkloadPackKind.Template);
            templateItem.Path.Should().Be(Path.Combine("fakepath", "template-packs", "xamarin.android.templates.1.0.3.nupkg"));
        }

        [Fact]
        public void GivienTemplateNupkgDoesNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver = new WorkloadResolver(new FakeManifestProvider(new[] { Path.Combine("Manifests", "Sample.json") }), "fakepath");
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => false, directoryExists: (_) => true);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(0);
        }
    }
}
