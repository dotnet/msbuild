// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.Build.Containers;

internal class Layer
{
    // NOTE: The SID string below was created using the following snippet. As the code is Windows only we keep the constant
    // private static string CreateUserOwnerAndGroupSID()
    // {
    //     var descriptor = new RawSecurityDescriptor(
    //         ControlFlags.SelfRelative,
    //         new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
    //         new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
    //         null,
    //         null
    //     );
    //
    //     var raw = new byte[descriptor.BinaryLength];
    //     descriptor.GetBinaryForm(raw, 0);
    //     return Convert.ToBase64String(raw);
    // }

    private const string BuiltinUsersSecurityDescriptor = "AQAAgBQAAAAkAAAAAAAAAAAAAAABAgAAAAAABSAAAAAhAgAAAQIAAAAAAAUgAAAAIQIAAA==";

    public virtual Descriptor Descriptor { get; }

    public string BackingFile { get; }

    internal Layer()
    {
        Descriptor = new Descriptor();
        BackingFile = "";
    }
    internal Layer(string backingFile, Descriptor descriptor)
    {
        BackingFile = backingFile;
        Descriptor = descriptor;
    }

    public static Layer FromDescriptor(Descriptor descriptor)
    {
        return new(ContentStore.PathForDescriptor(descriptor), descriptor);
    }

    public static Layer FromDirectory(string directory, string containerPath, bool isWindowsLayer)
    {
        var fileList =
            new DirectoryInfo(directory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(fsi =>
                    {
                        string destinationPath = Path.Join(containerPath, Path.GetRelativePath(directory, fsi.FullName)).Replace(Path.DirectorySeparatorChar, '/');
                        return (fsi.FullName, destinationPath);
                    });
        return FromFiles(fileList, isWindowsLayer);
    }

    public static Layer FromFiles(IEnumerable<(string path, string containerPath)> fileList, bool isWindowsLayer)
    {
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> uncompressedHash = stackalloc byte[SHA256.HashSizeInBytes];

        // this factory helps us creating the Tar entries with the right attributes
        PaxTarEntry CreateTarEntry(TarEntryType entryType, string containerPath)
        {
            var extendedAttributes = new Dictionary<string, string>();
            if (isWindowsLayer)
            {
                // We grant all users access to the application directory
                // https://github.com/buildpacks/rfcs/blob/main/text/0076-windows-security-identifiers.md
                extendedAttributes["MSWINDOWS.rawsd"] = BuiltinUsersSecurityDescriptor;
                return new PaxTarEntry(entryType, containerPath, extendedAttributes);
            }

            var entry = new PaxTarEntry(entryType, containerPath, extendedAttributes)
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute
            };
            return entry;
        }

        string SanitizeContainerPath(string containerPath)
        {
            // no leading slashes
            containerPath = containerPath.TrimStart(PathSeparators);

            // For Windows layers we need to put files into a "Files" directory without drive letter.
            if (isWindowsLayer)
            {
                // Cut of drive letter:  /* C:\ */
                if (containerPath[1] == ':')
                {
                    containerPath = containerPath[3..];
                }

                containerPath = "Files/" + containerPath;
            }

            return containerPath;
        }

        // Ensures that all directory entries for the given segments are created within the tar.
        var directoryEntries = new HashSet<string>();
        void EnsureDirectoryEntries(TarWriter tar,
            IReadOnlyList<string> filePathSegments)
        {
            var pathBuilder = new StringBuilder();
            for (int i = 0; i < filePathSegments.Count - 1; i++)
            {
                pathBuilder.Append(CultureInfo.InvariantCulture, $"{filePathSegments[i]}/");

                string fullPath = pathBuilder.ToString();
                if (!directoryEntries.Contains(fullPath))
                {
                    tar.WriteEntry(CreateTarEntry(TarEntryType.Directory, fullPath));
                    directoryEntries.Add(fullPath);
                }
            }
        }


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
                        string containerPath = SanitizeContainerPath(item.containerPath);

                        EnsureDirectoryEntries(writer, containerPath.Split(PathSeparators));

                        using var fileStream = File.OpenRead(item.path);
                        var entry = CreateTarEntry(TarEntryType.RegularFile, containerPath);
                        entry.DataStream = fileStream;

                        writer.WriteEntry(entry);
                    }

                    // Windows layers need a Hives folder, we do not need to create any Registry Hive deltas inside
                    if (isWindowsLayer)
                    {
                        var entry = CreateTarEntry(TarEntryType.Directory, "Hives/");
                        writer.WriteEntry(entry);
                    }

                } // Dispose of the TarWriter before getting the hash so the final data get written to the tar stream

                int bytesWritten = gz.GetCurrentUncompressedHash(uncompressedHash);
                Debug.Assert(bytesWritten == uncompressedHash.Length);
            }

            fileSize = fs.Length;

            fs.Position = 0;

            int bW = SHA256.HashData(fs, hash);
            Debug.Assert(bW == hash.Length);
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

        return new(storedContent, descriptor);
    }

    internal virtual Stream OpenBackingFile() => File.OpenRead(BackingFile);

    private readonly static char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

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
