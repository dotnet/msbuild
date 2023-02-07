// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.Build.Containers;

public record struct Layer
{
    public Descriptor Descriptor { get; private set; }

    public string BackingFile { get; private set; }

    public static Layer FromDescriptor(Descriptor descriptor)
    {
        return new()
        {
            BackingFile = ContentStore.PathForDescriptor(descriptor),
            Descriptor = descriptor
        };
    }

    public static Layer FromDirectory(string directory, string containerPath)
    {
        var fileList =
            new DirectoryInfo(directory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(fsi =>
                    {
                        string destinationPath = Path.Join(containerPath, Path.GetRelativePath(directory, fsi.FullName)).Replace(Path.DirectorySeparatorChar, '/');
                        return (fsi.FullName, destinationPath);
                    });
        return FromFiles(fileList);
    }

    public static Layer FromFiles(IEnumerable<(string path, string containerPath)> fileList)
    {
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> uncompressedHash = stackalloc byte[SHA256.HashSizeInBytes];

        var directoryEntries = new HashSet<string>();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using (HashDigestGZipStream gz = new(fs, leaveOpen: true))
            {
                using (TarWriter writer = new(gz, TarEntryFormat.Pax, leaveOpen: true))
                {
                    foreach (var item in fileList)
                    {
                        // Docker treats a COPY instruction that copies to a path like `/app` by
                        // including `app/` as a directory, with no leading slash. Emulate that here.
                        string containerPath = item.containerPath.TrimStart(PathSeparators);

                        EnsureDirectoryEntries(writer, directoryEntries, containerPath.Split(PathSeparators));

                        writer.WriteEntry(item.path, containerPath);
                    }
                } // Dispose of the TarWriter before getting the hash so the final data get written to the tar stream

                int bytesWritten = gz.GetCurrentUncompressedHash(uncompressedHash);
                Debug.Assert(bytesWritten == uncompressedHash.Length);
            }

            fileSize = fs.Length;

            fs.Position = 0;

            SHA256.HashData(fs, hash);
        }

        string contentHash = Convert.ToHexString(hash).ToLowerInvariant();
        string uncompressedContentHash = Convert.ToHexString(uncompressedHash).ToLowerInvariant();

        Descriptor descriptor = new()
        {
            MediaType = "application/vnd.docker.image.rootfs.diff.tar.gzip", // TODO: configurable? gzip always?
            Size = fileSize,
            Digest = $"sha256:{contentHash}",
            UncompressedDigest = $"sha256:{uncompressedContentHash}",
        };

        string storedContent = ContentStore.PathForDescriptor(descriptor);

        Directory.CreateDirectory(ContentStore.ContentRoot);

        File.Move(tempTarballPath, storedContent, overwrite: true);

        Layer l = new()
        {
            Descriptor = descriptor,
            BackingFile = storedContent,
        };

        return l;
    }

    private readonly static char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    /// <summary>
    /// Ensures that all directory entries for the given segments are created within the tar.
    /// </summary>
    /// <param name="tar">The tar into which to add the directory entries.</param>
    /// <param name="directoryEntries">The lookup of all known directory entries. </param>
    /// <param name="filePathSegments">The segments of the file within the tar for which to create the folders</param>
    private static void EnsureDirectoryEntries(TarWriter tar, HashSet<string> directoryEntries, IReadOnlyList<string> filePathSegments)
    {
        var pathBuilder = new StringBuilder();
        for (var i = 0; i < filePathSegments.Count - 1; i++)
        {
            pathBuilder.Append($"{filePathSegments[i]}/");

            var fullPath = pathBuilder.ToString(); 
            if (!directoryEntries.Contains(fullPath))
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, fullPath));
                directoryEntries.Add(fullPath);
            }
        }
    }

    /// <summary>
    /// A stream capable of computing the hash digest of raw uncompressed data while also compressing it.
    /// </summary>
    private sealed class HashDigestGZipStream : Stream
    {
        private readonly IncrementalHash sha256Hash;
        private readonly GZipStream compressionStream;

        public HashDigestGZipStream(Stream writeStream, bool leaveOpen)
        {
            sha256Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            compressionStream = new GZipStream(writeStream, CompressionMode.Compress, leaveOpen);
        }

        public override bool CanWrite => true;

        public override void Write(byte[] buffer, int offset, int count)
        {
            sha256Hash.AppendData(buffer, offset, count);
            compressionStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            sha256Hash.AppendData(buffer);
            compressionStream.Write(buffer);
        }

        public override void Flush()
        {
            compressionStream.Flush();
        }

        internal int GetCurrentUncompressedHash(Span<byte> buffer) => sha256Hash.GetCurrentHash(buffer);

        protected override void Dispose(bool disposing)
        {
            try
            {
                sha256Hash.Dispose();
                compressionStream.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // This class is never used with async writes, but if it ever is, implement these overrides
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }
}
