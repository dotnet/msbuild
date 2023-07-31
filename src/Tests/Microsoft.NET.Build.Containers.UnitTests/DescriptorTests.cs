// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DescriptorTests
{
    [Fact]
    public void BasicConstructor()
    {
        Descriptor d = new(
            mediaType: "application/vnd.oci.image.manifest.v1+json",
            digest: "sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270",
            size: 7682);

        Console.WriteLine(JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal("application/vnd.oci.image.manifest.v1+json", d.MediaType);
        Assert.Equal("sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270", d.Digest);
        Assert.Equal(7_682, d.Size);

        Assert.Null(d.Annotations);
        Assert.Null(d.Data);
        Assert.Null(d.Urls);
    }
}
