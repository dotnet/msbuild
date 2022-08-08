using System.Diagnostics;

namespace Microsoft.NET.Build.Containers;

public static class ContentStore
{
    public static string ArtifactRoot { get; set; } = Path.Combine(Path.GetTempPath(), "Containers");
    public static string ContentRoot
    {
        get => Path.Combine(ArtifactRoot, "Content");
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

        Debug.Assert(digest.StartsWith("sha256:"));

        string contentHash = digest.Substring("sha256:".Length);

        string extension = descriptor.MediaType switch
        {
            "application/vnd.docker.image.rootfs.diff.tar.gzip"
            or "application/vnd.docker.image.rootfs.diff.tar"
            or "application/vnd.oci.image.layer.v1.tar"
            or "application/vnd.oci.image.layer.v1.tar+gzip"
                => ".tar",
            _ => throw new ArgumentException($"Unrecognized mediaType '{descriptor.MediaType}'")
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
