// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.IO.Enumeration;

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
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> uncompressedHash = stackalloc byte[SHA256.HashSizeInBytes];

        // Docker treats a COPY instruction that copies to a path like `/app` by
        // including `app/` as a directory, with no leading slash. Emulate that here.
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

        // Trim training path separator (if present).
        containerPath = containerPath.TrimEnd(PathSeparators);

        // Use only '/' as directory separator.
        containerPath = containerPath.Replace('\\', '/');

        var entryAttributes = new Dictionary<string, string>();
        if (isWindowsLayer)
        {
            // We grant all users access to the application directory
            // https://github.com/buildpacks/rfcs/blob/main/text/0076-windows-security-identifiers.md
            entryAttributes["MSWINDOWS.rawsd"] = BuiltinUsersSecurityDescriptor;
        }

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using (HashDigestGZipStream gz = new(fs, leaveOpen: true))
            {
                using (TarWriter writer = new(gz, TarEntryFormat.Pax, leaveOpen: true))
                {
                    // Windows layers need a Files folder
                    if (isWindowsLayer)
                    {
                        var entry = new PaxTarEntry(TarEntryType.Directory, "Files", entryAttributes);
                        writer.WriteEntry(entry);
                    }

                    // Write an entry for the application directory.
                    WriteTarEntryForFile(writer, new DirectoryInfo(directory), containerPath, entryAttributes);

                    // Write entries for the application directory contents.
                    var fileList = new FileSystemEnumerable<(FileSystemInfo file, string containerPath)>(
                                directory: directory,
                                transform: (ref FileSystemEntry entry) =>
                                {
                                    FileSystemInfo fsi = entry.ToFileSystemInfo();
                                    string relativePath = Path.GetRelativePath(directory, fsi.FullName);
                                    if (OperatingSystem.IsWindows())
                                    {
                                        // Use only '/' directory separators.
                                        relativePath = relativePath.Replace('\\', '/');
                                    }
                                    return (fsi, $"{containerPath}/{relativePath}");
                                },
                                options: new EnumerationOptions()
                                {
                                    RecurseSubdirectories = true
                                });
                    foreach (var item in fileList)
                    {
                        WriteTarEntryForFile(writer, item.file, item.containerPath, entryAttributes);
                    }

                    // Windows layers need a Hives folder, we do not need to create any Registry Hive deltas inside
                    if (isWindowsLayer)
                    {
                        var entry = new PaxTarEntry(TarEntryType.Directory, "Hives", entryAttributes);
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

            // Writes a tar entry corresponding to the file system item.
            static void WriteTarEntryForFile(TarWriter writer, FileSystemInfo file, string containerPath, IEnumerable<KeyValuePair<string, string>> entryAttributes)
            {
                UnixFileMode mode = DetermineFileMode(file);

                if (file is FileInfo)
                {
                    using var fileStream = File.OpenRead(file.FullName);
                    PaxTarEntry entry = new(TarEntryType.RegularFile, containerPath, entryAttributes)
                    {
                        Mode = mode,
                        DataStream = fileStream
                    };
                    writer.WriteEntry(entry);
                }
                else
                {
                    PaxTarEntry entry = new(TarEntryType.Directory, containerPath, entryAttributes)
                    {
                        Mode = mode
                    };
                    writer.WriteEntry(entry);
                }

                static UnixFileMode DetermineFileMode(FileSystemInfo file)
                {
                    const UnixFileMode nonExecuteMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                        UnixFileMode.GroupRead |
                                                        UnixFileMode.OtherRead;
                    const UnixFileMode executeMode = nonExecuteMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

                    // On Unix, we can determine the x-bit based on the filesystem permission.
                    // On Windows, we use executable permissions for all entries.
                    return (OperatingSystem.IsWindows() || ((file.UnixFileMode | UnixFileMode.UserExecute) != 0)) ? executeMode : nonExecuteMode;
                }
            }
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
