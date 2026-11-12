// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests;

public class ManifestWriter_Tests
{
    private static string TestAssetsRootPath { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "TestResources",
        "Manifests");

    [Fact]
    public void WriteManifestPreservesInputStream()
    {
        Manifest manifest = ManifestReader.ReadManifest(
            Path.Combine(TestAssetsRootPath, "buildIn.manifest"),
            preserveStream: true);
        manifest.InputStream.ShouldNotBeNull();
        using Stream inputStream = manifest.InputStream;
        using MemoryStream firstOutput = new();
        using MemoryStream secondOutput = new();

        ManifestWriter.WriteManifest(manifest, firstOutput);
        inputStream.CanRead.ShouldBeTrue();

        inputStream.Position = 0;
        ManifestWriter.WriteManifest(manifest, secondOutput);
        inputStream.CanRead.ShouldBeTrue();
        firstOutput.Length.ShouldBeGreaterThan(0);
        secondOutput.ToArray().ShouldBe(firstOutput.ToArray());
    }
}
