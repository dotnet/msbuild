// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can extract a tar archive, optionally compressed with GZip or ZStandard.
    /// </summary>
    /// <remarks>
    /// This task uses the <see cref="System.Formats.Tar"/> APIs which are only available when MSBuild
    /// runs on .NET (not .NET Framework). It is therefore registered to run only on the .NET runtime and
    /// is unavailable in Visual Studio / MSBuild.exe.
    /// </remarks>
    [MSBuildMultiThreadableTask]
    public sealed class Untar : TaskExtension, ICancelableTask, IIncrementalTask, IMultiThreadableTask
    {
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        private const int _DefaultCopyBufferSize = 81920;

        /// <summary>
        /// Stores a <see cref="CancellationTokenSource"/> used for cancellation.
        /// </summary>
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        /// <summary>
        /// Stores the include patterns after parsing.
        /// </summary>
        private string[] _includePatterns = [];

        /// <summary>
        /// Stores the exclude patterns after parsing.
        /// </summary>
        private string[] _excludePatterns = [];

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> with a destination folder path to untar the files to.
        /// </summary>
        [Required]
        public ITaskItem DestinationFolder { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value that indicates whether read-only files should be overwritten.
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether files should be skipped if the destination is unchanged.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem"/> objects containing the paths to tar archive files to untar.
        /// The compression (none, GZip, or ZStandard) is detected automatically from the archive contents.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; } = null!;

        /// <summary>
        /// Gets or sets an MSBuild glob expression that specifies which files to include being untarred from the archive.
        /// </summary>
        public string? Include { get; set; }

        /// <summary>
        /// Gets or sets an MSBuild glob expression that specifies which files to exclude from being untarred from the archive.
        /// </summary>
        public string? Exclude { get; set; }

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        /// <inheritdoc cref="ICancelableTask.Cancel"/>
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }

        /// <inheritdoc cref="Task.Execute"/>
        public override bool Execute()
        {
            DirectoryInfo destinationDirectory;
            try
            {
                AbsolutePath destinationPath = TaskEnvironment.GetAbsolutePath(DestinationFolder.ItemSpec);
                destinationDirectory = Directory.CreateDirectory(destinationPath);
            }
            catch (Exception e)
            {
                Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotCreateDestinationDirectory", DestinationFolder.ItemSpec, e.Message);

                return false;
            }

            BuildEngine3.Yield();

            try
            {
                ParseIncludeExclude();

                if (!Log.HasLoggedErrors)
                {
                    foreach (ITaskItem sourceFile in SourceFiles.TakeWhile(i => !_cancellationToken.IsCancellationRequested))
                    {
                        AbsolutePath sourceFilePath;
                        try
                        {
                            sourceFilePath = TaskEnvironment.GetAbsolutePath(sourceFile.ItemSpec);
                        }
                        catch (Exception)
                        {
                            Log.LogErrorWithCodeFromResources("Untar.ErrorFileDoesNotExist", sourceFile.ItemSpec);
                            continue;
                        }

                        if (!FileSystems.Default.FileExists(sourceFilePath))
                        {
                            Log.LogErrorWithCodeFromResources("Untar.ErrorFileDoesNotExist", sourceFile.ItemSpec);
                            continue;
                        }

                        try
                        {
                            using FileStream stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0x1000, useAsync: false);

                            // Detect and unwrap any compression applied to the tar archive. The decompression stream (if any)
                            // and the TarReader are disposed by the enclosing using statements below.
                            using Stream? decompressionStream = CreateDecompressionStream(stream);
#pragma warning disable CA2000 // Dispose objects before losing scope because the using declaration disposes the TarReader.
                            using TarReader reader = new TarReader(decompressionStream ?? stream, leaveOpen: true);
#pragma warning restore CA2000 // Dispose objects before losing scope

                            try
                            {
                                Extract(reader, destinationDirectory);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception e)
                            {
                                // Should only be thrown if the archive could not be read (corrupt file, etc).
                                Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotOpenFile", sourceFile.ItemSpec, e.Message);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception e)
                        {
                            // Should only be thrown if the archive could not be opened (Access denied, corrupt file, etc).
                            Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotOpenFile", sourceFile.ItemSpec, e.Message);
                        }
                    }
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !_cancellationToken.IsCancellationRequested && !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Creates a decompression stream around <paramref name="stream"/> if the archive is compressed.
        /// </summary>
        /// <param name="stream">The seekable <see cref="FileStream"/> positioned at the start of the archive.</param>
        /// <returns>
        /// A decompression <see cref="Stream"/> when GZip or ZStandard compression is detected; otherwise <see langword="null"/>,
        /// in which case the archive should be read directly from <paramref name="stream"/>.
        /// </returns>
        private static Stream? CreateDecompressionStream(FileStream stream)
        {
            Span<byte> magic = stackalloc byte[4];
            int read = stream.Read(magic);
            stream.Position = 0;

            // GZip magic number: 0x1F 0x8B.
            if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
            {
                return new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            }

            // ZStandard magic number: 0x28 0xB5 0x2F 0xFD.
            if (read >= 4 && magic[0] == 0x28 && magic[1] == 0xB5 && magic[2] == 0x2F && magic[3] == 0xFD)
            {
                return new ZstandardStream(stream, CompressionMode.Decompress, leaveOpen: true);
            }

            return null;
        }

        /// <summary>
        /// Extracts all entries to the specified directory.
        /// </summary>
        /// <param name="reader">The <see cref="TarReader"/> containing the entries to extract.</param>
        /// <param name="destinationDirectory">The <see cref="DirectoryInfo"/> to extract entries to.</param>
        private void Extract(TarReader reader, DirectoryInfo destinationDirectory)
        {
            AbsolutePath fullDestinationDirectoryPath = TaskEnvironment.GetAbsolutePath(FileUtilities.EnsureTrailingSlash(destinationDirectory.FullName)).GetCanonicalForm();

            for (TarEntry? tarEntry = reader.GetNextEntry(); tarEntry is not null && !_cancellationToken.IsCancellationRequested; tarEntry = reader.GetNextEntry())
            {
                string entryName = tarEntry.Name;

                if (ShouldSkipEntry(entryName))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfFilter", entryName);
                    continue;
                }

                AbsolutePath fullDestinationPath = TaskEnvironment.GetAbsolutePath(Path.Combine(destinationDirectory.FullName, entryName)).GetCanonicalForm();
                ErrorUtilities.VerifyThrowInvalidOperation(fullDestinationPath.Value.StartsWith(fullDestinationDirectoryPath, FileUtilities.PathComparison), "Untar.TarSlipExploit", fullDestinationPath);

                FileInfo destinationPath = new(fullDestinationPath);

                // Directory entries and entries whose name refers to a directory should be created and skipped.
                if (tarEntry.EntryType is TarEntryType.Directory || Path.GetFileName(destinationPath.FullName).Length == 0)
                {
                    Directory.CreateDirectory(destinationPath.FullName);
                    continue;
                }

                // Only regular files are extracted. Other entry types (symbolic/hard links, devices, etc.) are skipped.
                if (tarEntry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfEntryType", entryName, tarEntry.EntryType.ToString());
                    continue;
                }

                if (!destinationPath.FullName.StartsWith(destinationDirectory.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    // TarFile.ExtractToDirectory() throws an IOException for this but since we're extracting one file at a time
                    // for logging and cancellation, we need to check for it ourselves.
                    Log.LogErrorFromResources("Untar.ErrorExtractingResultsInFilesOutsideDestination", destinationPath.FullName, destinationDirectory.FullName);
                    continue;
                }

                if (ShouldSkipEntry(tarEntry, destinationPath))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfFileMatch", entryName, destinationPath.FullName, nameof(SkipUnchangedFiles), "true");
                    continue;
                }
                else if (FailIfNotIncremental)
                {
                    Log.LogErrorFromResources("Untar.FileComment", entryName, destinationPath.FullName);
                    continue;
                }

                try
                {
                    destinationPath.Directory?.Create();
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotCreateDestinationDirectory", destinationPath.DirectoryName, e.Message);
                    continue;
                }

                if (OverwriteReadOnlyFiles && destinationPath.Exists && destinationPath.IsReadOnly)
                {
                    try
                    {
                        destinationPath.IsReadOnly = false;
                    }
                    catch (Exception e)
                    {
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(destinationPath.FullName);
                        Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotMakeFileWriteable", entryName, destinationPath.FullName, e.Message, lockedFileMessage);
                        continue;
                    }
                }

                try
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "Untar.FileComment", entryName, destinationPath.FullName);

                    FileStreamOptions fileStreamOptions = new()
                    {
                        Access = FileAccess.Write,
                        Mode = FileMode.Create,
                        Share = FileShare.None,
                        BufferSize = 0x1000
                    };

                    const UnixFileMode OwnershipPermissions =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

                    // Restore Unix permissions.
                    // For security, limit to ownership permissions, and respect umask (through UnixCreateMode).
                    UnixFileMode mode = tarEntry.Mode & OwnershipPermissions;
                    if (mode != UnixFileMode.None && NativeMethodsShared.IsUnixLike)
                    {
                        fileStreamOptions.UnixCreateMode = mode;
                    }

                    using (FileStream destination = new FileStream(destinationPath.FullName, fileStreamOptions))
                    {
                        if (tarEntry.DataStream is Stream dataStream)
                        {
#pragma warning disable CA2025 // Do not pass 'IDisposable' instances into unawaited tasks
                            dataStream.CopyToAsync(destination, _DefaultCopyBufferSize, _cancellationToken.Token)
                                .ConfigureAwait(continueOnCapturedContext: false)
                                .GetAwaiter()
                                .GetResult();
#pragma warning restore CA2025
                        }
                    }

                    destinationPath.LastWriteTimeUtc = tarEntry.ModificationTime.UtcDateTime;
                }
                catch (IOException e)
                {
                    Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotExtractFile", entryName, destinationPath.FullName, e.Message);
                }
            }
        }

        /// <summary>
        /// Determines whether or not an entry should be skipped when untarring by filtering.
        /// </summary>
        /// <param name="entryName">The full name of the entry in the tar archive.</param>
        /// <returns><code>true</code> if the entry should be skipped, otherwise <code>false</code>.</returns>
        private bool ShouldSkipEntry(string entryName)
        {
            bool result = false;

            if (_includePatterns.Length > 0)
            {
                result = _includePatterns.All(pattern => !FileMatcher.IsMatch(FileMatcher.Normalize(entryName), pattern));
            }

            if (_excludePatterns.Length > 0)
            {
                result |= _excludePatterns.Any(pattern => FileMatcher.IsMatch(FileMatcher.Normalize(entryName), pattern));
            }

            return result;
        }

        /// <summary>
        /// Determines whether or not an entry should be skipped when untarring.
        /// </summary>
        /// <param name="tarEntry">The <see cref="TarEntry"/> object containing information about the entry in the tar archive.</param>
        /// <param name="fileInfo">A <see cref="FileInfo"/> object containing information about the destination file.</param>
        /// <returns><code>true</code> if the entry should be skipped, otherwise <code>false</code>.</returns>
        private bool ShouldSkipEntry(TarEntry tarEntry, FileInfo fileInfo)
        {
            return SkipUnchangedFiles
                   && fileInfo.Exists
                   && tarEntry.ModificationTime.UtcDateTime == fileInfo.LastWriteTimeUtc
                   && tarEntry.Length == fileInfo.Length;
        }

        private void ParseIncludeExclude()
        {
            ParsePattern(Include, out _includePatterns);
            ParsePattern(Exclude, out _excludePatterns);
        }

        private void ParsePattern(string? pattern, out string[] patterns)
        {
            patterns = [];
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                if (FileMatcher.HasPropertyOrItemReferences(pattern))
                {
                    // Supporting property references would require access to Expander which is unavailable in Microsoft.Build.Tasks
                    Log.LogErrorWithCodeFromResources("Untar.ErrorParsingPatternPropertyReferences", pattern);
                }
                else if (pattern.AsSpan().IndexOfAny(FileUtilities.InvalidPathChars) >= 0)
                {
                    Log.LogErrorWithCodeFromResources("Untar.ErrorParsingPatternInvalidPath", pattern);
                }
                else
                {
                    patterns = pattern.Contains(';')
                                   ? pattern.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(FileMatcher.Normalize).ToArray()
                                   : [pattern];
                }
            }
        }
    }
}
