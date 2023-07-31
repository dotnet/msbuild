// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal static class ContentStore
{
    public static string ArtifactRoot { get; set; } = Path.Combine(Path.GetTempPath(), "Containers");
    public static string ContentRoot
    {
        get
        {
            string contentPath = Path.Join(ArtifactRoot, "Content");

            Directory.CreateDirectory(contentPath);

            return contentPath;
        }
    }

    public static string TempPath
    {
        get
        {
            string tempPath = Path.Join(ArtifactRoot, "Temp");

            Directory.CreateDirectory(tempPath);

            return tempPath;
        }
    }

    public static string PathForDescriptor(Descriptor descriptor)
    {
        string digest = descriptor.Digest;

        Debug.Assert(digest.StartsWith("sha256:", StringComparison.Ordinal));

        string contentHash = digest.Substring("sha256:".Length);

        string extension = descriptor.MediaType switch
        {
            "application/vnd.docker.image.rootfs.diff.tar.gzip"
            or "application/vnd.oci.image.layer.v1.tar+gzip"
            or "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip"
                => ".tar.gz",
            "application/vnd.docker.image.rootfs.diff.tar"
            or "application/vnd.oci.image.layer.v1.tar"
                => ".tar",
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnrecognizedMediaType), descriptor.MediaType))
        };

        return GetPathForHash(contentHash) + extension;
    }


    public static string GetPathForHash(string contentHash)
    {
        return Path.Combine(ContentRoot, contentHash);
    }

    public static string GetTempFile()
    {
        return Path.Join(TempPath, Path.GetRandomFileName());
    }
}
