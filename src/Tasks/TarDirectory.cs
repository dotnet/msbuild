// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can create a tar archive from a directory.
    /// </summary>
    /// <remarks>
    /// This task uses the <see cref="System.Formats.Tar"/> APIs which are only available when MSBuild
    /// runs on .NET (not .NET Framework). It is therefore registered to run only on the .NET runtime and
    /// is unavailable in Visual Studio / MSBuild.exe.
    /// </remarks>
    [MSBuildMultiThreadableTask]
    public sealed class TarDirectory : TaskExtension, IIncrementalTask, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the full path to the destination file to create.
        /// </summary>
        [Required]
        public FileInfo DestinationFile { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the destination file should be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Gets or sets the full path to the source directory to create a tar archive from.
        /// </summary>
        [Required]
        public DirectoryInfo SourceDirectory { get; set; } = null!;

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Gets or sets the compression to apply to the tar archive.
        /// The default is <see cref="TarCompression.None"/>.
        /// This parameter is optional.
        /// </summary>
        public TarCompression Compression { get; set; } = TarCompression.None;

        /// <summary>
        /// Gets or sets the tar entry format to use for the archive.
        /// The default is <see cref="TarEntryFormat.Pax"/>.
        /// This parameter is optional.
        /// </summary>
        public TarEntryFormat Format { get; set; } = TarEntryFormat.Pax;

        /// <summary>
        /// Gets or sets an optional timestamp to stamp on every entry in the archive in place of the source files'
        /// last-write times. Supplying this value makes the produced archive reproducible across machines and runs.
        /// The value may be an RFC 3339 date-time (for example, <c>2024-01-01T00:00:00Z</c>) or an integer number of
        /// seconds since the Unix epoch (for example, <c>1704067200</c>, which is also the form of <c>SOURCE_DATE_EPOCH</c>).
        /// When empty, each entry keeps its source file's last-write time (entries are always written in a
        /// deterministic order regardless of this value).
        /// This parameter is optional.
        /// </summary>
        public string DeterministicTimestamp { get; set; } = string.Empty;

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public override bool Execute()
        {
            if (!SourceDirectory.Exists)
            {
                Log.LogErrorWithCodeFromResources("TarDirectory.ErrorDirectoryDoesNotExist", SourceDirectory.FullName);
                return false;
            }

            // Evaluate all preconditions before yielding so that failures (which do no real work) don't
            // pay the cost of yielding and reacquiring the build engine node.
            if (DestinationFile.Exists)
            {
                if (!Overwrite || FailIfNotIncremental)
                {
                    Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFileExists", DestinationFile.FullName);

                    return false;
                }

                try
                {
                    File.Delete(DestinationFile.FullName);
                }
                catch (Exception e)
                {
                    string lockedFileMessage = LockCheck.GetLockedFileMessage(DestinationFile.FullName);
                    Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", SourceDirectory.FullName, DestinationFile.FullName, e.Message, lockedFileMessage);

                    return false;
                }
            }

            if (FailIfNotIncremental)
            {
                Log.LogErrorFromResources("TarDirectory.Comment", SourceDirectory.FullName, DestinationFile.FullName);

                return false;
            }

            DateTimeOffset? deterministicTimestamp = null;
            if (!string.IsNullOrEmpty(DeterministicTimestamp))
            {
                if (!TryParseTimestamp(DeterministicTimestamp, out DateTimeOffset parsedTimestamp))
                {
                    Log.LogErrorWithCodeFromResources("TarDirectory.InvalidDeterministicTimestamp", DeterministicTimestamp);

                    return false;
                }

                deterministicTimestamp = parsedTimestamp;
            }

            BuildEngine3.Yield();

            try
            {
                Log.LogMessageFromResources(MessageImportance.High, "TarDirectory.Comment", SourceDirectory.FullName, DestinationFile.FullName);

                // Unknown is only reachable if it was explicitly set; fall back to the Pax default.
                TarEntryFormat format = Format == TarEntryFormat.Unknown ? TarEntryFormat.Pax : Format;

                // The destination is guaranteed not to exist at this point: Execute deletes any existing
                // file (or errors when Overwrite is false) before yielding, so OpenWrite creates a fresh file.
                using FileStream destinationStream = DestinationFile.OpenWrite();

                // Wrap the destination stream in the requested compression, if any. The tar archive is always
                // written to the (optionally compressed) stream, and the TarWriter is created with the requested
                // TarEntryFormat so every entry is emitted in that format.
                using Stream? compressionStream = Compression switch
                {
                    TarCompression.GZip => new GZipStream(destinationStream, CompressionLevel.Optimal),
                    TarCompression.ZStandard => new ZstandardStream(destinationStream, CompressionLevel.Optimal),
                    _ => null,
                };

                // Write the archive entry-by-entry (rather than the one-shot TarFile.CreateFromDirectory) so that the
                // entries are emitted in a deterministic, ordinal-sorted order. When a deterministic timestamp is
                // supplied, each entry is constructed manually so its modification time can be overridden; otherwise
                // per-entry metadata is written exactly as TarFile.CreateFromDirectory would via WriteEntry.
                using TarWriter writer = new TarWriter(compressionStream ?? destinationStream, format, leaveOpen: true);

                foreach ((FileSystemInfo info, string entryName) in EnumerateEntriesInDeterministicOrder())
                {
                    if (deterministicTimestamp is DateTimeOffset timestamp)
                    {
                        WriteStampedEntry(writer, format, info, entryName, timestamp);
                    }
                    else
                    {
                        writer.WriteEntry(info.FullName, entryName);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", SourceDirectory.FullName, DestinationFile.FullName, e.Message, string.Empty);
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Enumerates every filesystem entry under <see cref="SourceDirectory"/> paired with the name it should be
        /// given inside the archive, sorted by entry name using an ordinal comparison so that the archive is written
        /// in a deterministic, reproducible order regardless of how the underlying filesystem enumerates directory
        /// contents. This mirrors the entry naming of <see cref="TarFile.CreateFromDirectory(string, Stream, bool, TarEntryFormat)"/>
        /// (relative, forward-slash separated, directories suffixed with '/', base directory excluded).
        /// </summary>
        private List<(FileSystemInfo Info, string EntryName)> EnumerateEntriesInDeterministicOrder()
        {
            string basePath = FileUtilities.EnsureTrailingSlash(SourceDirectory.FullName);

            List<(FileSystemInfo Info, string EntryName)> entries = [];
            CollectEntries(SourceDirectory, basePath, entries);

            // Order determinism: sort by the in-archive entry name using an ordinal comparison. Because a
            // directory's entry name ends in '/', it is always a prefix of the names of everything it contains,
            // so directories sort ahead of their own contents, preserving the parent-before-children ordering
            // that tar expects for restoring directory timestamps.
            entries.Sort(static (left, right) => string.CompareOrdinal(left.EntryName, right.EntryName));

            return entries;
        }

        /// <summary>
        /// Recursively collects the filesystem entries under <paramref name="directory"/> into <paramref name="entries"/>.
        /// Directory symlinks and junctions are recorded as entries but are not recursed into, matching the behavior of
        /// <see cref="TarFile.CreateFromDirectory(string, Stream, bool, TarEntryFormat)"/> and avoiding reparse-point cycles.
        /// </summary>
        private static void CollectEntries(DirectoryInfo directory, string basePath, List<(FileSystemInfo Info, string EntryName)> entries)
        {
            foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
            {
                bool isRealDirectory = info is DirectoryInfo && (info.Attributes & FileAttributes.ReparsePoint) == 0;

                string relativePath = info.FullName.Substring(basePath.Length).Replace('\\', '/');
                entries.Add((info, isRealDirectory ? relativePath + "/" : relativePath));

                if (isRealDirectory)
                {
                    CollectEntries((DirectoryInfo)info, basePath, entries);
                }
            }
        }

        /// <summary>
        /// Parses a <see cref="DeterministicTimestamp"/> value. The value may be an integer number of seconds since the
        /// Unix epoch, or an RFC 3339 date-time. Parsing is culture-invariant and always resolves to a UTC instant.
        /// </summary>
        private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
        {
            // A bare integer is interpreted as the number of seconds since the Unix epoch. This matches the
            // SOURCE_DATE_EPOCH convention and NuGet's deterministic-timestamp handling.
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long unixTimeSeconds))
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);

                return true;
            }

            return DateTimeOffset.TryParseExact(value, s_timestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);
        }

        /// <summary>
        /// Writes a single filesystem entry to the archive, stamping it with <paramref name="timestamp"/> in place of the
        /// source file's last-write time. The entry is constructed manually (rather than via <see cref="TarWriter.WriteEntry(string, string)"/>)
        /// so its modification time can be overridden. The source file's Unix mode is preserved; Unix owner ids default
        /// to 0, which both matches Windows behavior and is the conventional choice for a reproducible archive.
        /// </summary>
        private static void WriteStampedEntry(TarWriter writer, TarEntryFormat format, FileSystemInfo info, string entryName, DateTimeOffset timestamp)
        {
            bool isSymbolicLink = (info.Attributes & FileAttributes.ReparsePoint) != 0;
            bool isDirectory = info is DirectoryInfo && !isSymbolicLink;

            TarEntryType entryType = (isDirectory, isSymbolicLink, format) switch
            {
                (true, _, _) => TarEntryType.Directory,
                (_, true, _) => TarEntryType.SymbolicLink,
                (_, _, TarEntryFormat.V7) => TarEntryType.V7RegularFile,
                _ => TarEntryType.RegularFile,
            };

            TarEntry entry = CreateEntry(format, entryType, entryName);
            entry.ModificationTime = timestamp;

            // Preserve the source's Unix permissions (for example, executable bits). This information does not exist on
            // Windows, where the entry keeps the default mode for its type.
            if (!isSymbolicLink && !OperatingSystem.IsWindows())
            {
                entry.Mode = info.UnixFileMode;
            }

            FileStream? dataStream = null;
            try
            {
                if (isSymbolicLink)
                {
                    entry.LinkName = info.LinkTarget ?? string.Empty;
                }
                else if (!isDirectory)
                {
                    dataStream = ((FileInfo)info).OpenRead();
                    entry.DataStream = dataStream;
                }

                writer.WriteEntry(entry);
            }
            finally
            {
                dataStream?.Dispose();
            }
        }

        /// <summary>
        /// Creates a <see cref="TarEntry"/> of the concrete type that matches <paramref name="format"/>.
        /// </summary>
        private static TarEntry CreateEntry(TarEntryFormat format, TarEntryType entryType, string entryName) => format switch
        {
            TarEntryFormat.V7 => new V7TarEntry(entryType, entryName),
            TarEntryFormat.Ustar => new UstarTarEntry(entryType, entryName),
            TarEntryFormat.Gnu => new GnuTarEntry(entryType, entryName),
            _ => new PaxTarEntry(entryType, entryName),
        };

        /// <summary>
        /// The RFC 3339 date-time formats accepted for <see cref="DeterministicTimestamp"/>, mirroring NuGet's
        /// deterministic-timestamp parsing.
        /// </summary>
        private static readonly string[] s_timestampFormats =
        [
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:sszzz",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        ];

        /// <summary>
        /// Identifies the compression to apply to the tar archive stream.
        /// </summary>
        public enum TarCompression
        {
            None,
            GZip,
            ZStandard,
        }
    }
}
