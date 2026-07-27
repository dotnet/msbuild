// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
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
                // entries are emitted in a deterministic, ordinal-sorted order. Per-entry metadata is written exactly
                // as TarFile.CreateFromDirectory would via WriteEntry(fullPath, entryName); only the order is affected.
                using TarWriter writer = new TarWriter(compressionStream ?? destinationStream, format, leaveOpen: true);

                foreach ((string fullPath, string entryName) in EnumerateEntriesInDeterministicOrder())
                {
                    writer.WriteEntry(fullPath, entryName);
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
        private List<(string FullPath, string EntryName)> EnumerateEntriesInDeterministicOrder()
        {
            string basePath = FileUtilities.EnsureTrailingSlash(SourceDirectory.FullName);

            List<(string FullPath, string EntryName)> entries = [];
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
        private static void CollectEntries(DirectoryInfo directory, string basePath, List<(string FullPath, string EntryName)> entries)
        {
            foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
            {
                bool isRealDirectory = info is DirectoryInfo && (info.Attributes & FileAttributes.ReparsePoint) == 0;

                string relativePath = info.FullName.Substring(basePath.Length).Replace('\\', '/');
                entries.Add((info.FullName, isRealDirectory ? relativePath + "/" : relativePath));

                if (isRealDirectory)
                {
                    CollectEntries((DirectoryInfo)info, basePath, entries);
                }
            }
        }

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
