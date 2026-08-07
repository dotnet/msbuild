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
        /// <summary>
        /// Stores a <see cref="CancellationTokenSource"/> used for cancellation.
        /// </summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Stores the include patterns after parsing.
        /// </summary>
        private string[] _includePatterns = [];

        /// <summary>
        /// Stores the exclude patterns after parsing.
        /// </summary>
        private string[] _excludePatterns = [];

        /// <summary>
        /// Gets or sets a <see cref="DirectoryInfo"/> with a destination folder path to untar the files to.
        /// </summary>
        [Required]
        public DirectoryInfo DestinationFolder { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value that indicates whether read-only files should be overwritten.
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether files should be skipped if the destination is unchanged.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets an array of <see cref="FileInfo"/> objects containing the paths to tar archive files to untar.
        /// The compression (none, GZip, or ZStandard) is detected automatically from the archive contents.
        /// </summary>
        [Required]
        public FileInfo[] SourceFiles { get; set; } = null!;

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
            _cancellationTokenSource.Cancel();
        }

        /// <inheritdoc cref="Task.Execute"/>
        public override bool Execute()
        {
            // Bridge from the synchronous ITask.Execute entrypoint to the asynchronous implementation with a
            // single blocking call. The extraction pipeline is async so it can flow the cancellation token into
            // the runtime's asynchronous I/O; keeping the only GetAwaiter().GetResult() here (rather than in the
            // per-entry loop) avoids repeatedly blocking a thread-pool thread inside the loop.
            return ExecuteAsync()
                .ConfigureAwait(continueOnCapturedContext: false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Asynchronously extracts every source archive to the destination folder.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task{Boolean}"/> that resolves to <see langword="true"/> when extraction completed without errors or cancellation.</returns>
        private async System.Threading.Tasks.Task<bool> ExecuteAsync()
        {
            DirectoryInfo destinationDirectory;
            try
            {
                destinationDirectory = Directory.CreateDirectory(DestinationFolder.FullName);
            }
            catch (Exception e)
            {
                Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotCreateDestinationDirectory", DestinationFolder.FullName, e.Message);

                return false;
            }

            BuildEngine3.Yield();

            try
            {
                ParseIncludeExclude();

                if (!Log.HasLoggedErrors)
                {
                    foreach (FileInfo sourceFile in SourceFiles.TakeWhile(i => !_cancellationTokenSource.IsCancellationRequested))
                    {
                        if (!FileSystems.Default.FileExists(sourceFile.FullName))
                        {
                            Log.LogErrorWithCodeFromResources("Untar.ErrorFileDoesNotExist", sourceFile.FullName);
                            continue;
                        }

                        try
                        {
                            using FileStream stream = sourceFile.OpenRead();

                            // Detect and unwrap any compression applied to the tar archive. The decompression stream (if any)
                            // and the TarReader are disposed by the enclosing using statements below.
                            using Stream? decompressionStream = CreateDecompressionStream(stream);
#pragma warning disable CA2000 // Dispose objects before losing scope because the using declaration disposes the TarReader.
                            using TarReader reader = new TarReader(decompressionStream ?? stream, leaveOpen: true);
#pragma warning restore CA2000 // Dispose objects before losing scope

                            try
                            {
                                await ExtractAsync(reader, destinationDirectory).ConfigureAwait(continueOnCapturedContext: false);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception e)
                            {
                                // Should only be thrown if the archive could not be read (corrupt file, etc).
                                Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotOpenFile", sourceFile.FullName, e.Message);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception e)
                        {
                            // Should only be thrown if the archive could not be opened (Access denied, corrupt file, etc).
                            Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotOpenFile", sourceFile.FullName, e.Message);
                        }
                    }
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !_cancellationTokenSource.IsCancellationRequested && !Log.HasLoggedErrors;
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
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> that completes when all entries have been processed.</returns>
        private async System.Threading.Tasks.Task ExtractAsync(TarReader reader, DirectoryInfo destinationDirectory)
        {
            AbsolutePath fullDestinationDirectoryPath = TaskEnvironment.GetAbsolutePath(FileUtilities.EnsureTrailingSlash(destinationDirectory.FullName)).GetCanonicalForm();

            for (TarEntry? tarEntry = reader.GetNextEntry(); tarEntry is not null && !_cancellationTokenSource.IsCancellationRequested; tarEntry = reader.GetNextEntry())
            {
                string entryName = tarEntry.Name;

                if (ShouldSkipEntry(entryName))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfFilter", entryName);
                    continue;
                }

                AbsolutePath fullDestinationPath = TaskEnvironment.GetAbsolutePath(Path.Combine(destinationDirectory.FullName, entryName)).GetCanonicalForm();

                // Guard against tar-slip: an entry whose name contains ".." traversal segments (or an absolute path)
                // can resolve to a location outside the destination directory. Reject such entries and continue so
                // one malicious entry doesn't abort extraction of the rest of the (benign) archive.
                if (!fullDestinationPath.Value.StartsWith(fullDestinationDirectoryPath, FileUtilities.PathComparison))
                {
                    Log.LogErrorWithCodeFromResources("Untar.ErrorExtractingResultsInFilesOutsideDestination", fullDestinationPath.Value, fullDestinationDirectoryPath.Value);
                    continue;
                }

                FileInfo destinationPath = new(fullDestinationPath);

                // Directory entries and entries whose name refers to a directory should be created and skipped.
                if (tarEntry.EntryType is TarEntryType.Directory || Path.GetFileName(destinationPath.FullName).Length == 0)
                {
                    try
                    {
                        Directory.CreateDirectory(destinationPath.FullName);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        // Creating the directory can fail (e.g. a file already exists at that path, or permissions
                        // are denied). Report it against the entry and continue so one bad entry doesn't abort
                        // extraction of the rest of the archive.
                        Log.LogErrorWithCodeFromResources("Untar.ErrorCouldNotCreateDestinationDirectory", destinationPath.FullName, e.Message);
                    }

                    continue;
                }

                // Only regular files are extracted. Other entry types (symbolic/hard links, devices, etc.) are skipped.
                if (tarEntry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfEntryType", entryName, tarEntry.EntryType.ToString());
                    continue;
                }

                if (ShouldSkipEntry(tarEntry, destinationPath))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Untar.DidNotUntarBecauseOfFileMatch", entryName, destinationPath.FullName, nameof(SkipUnchangedFiles), "true");
                    continue;
                }
                else if (FailIfNotIncremental)
                {
                    Log.LogErrorWithCodeFromResources("Untar.ErrorFailIfNotIncremental", entryName, destinationPath.FullName);
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

                    // Delegate to the runtime's extraction, which restores the archived modification time and
                    // (on Unix) applies the archived permissions masked to the 9 ownership rwx bits, dropping the
                    // setuid/setgid/sticky bits for security and respecting the process umask. The cancellation
                    // token is flowed through so extraction stops promptly when the task is cancelled.
                    await tarEntry.ExtractToFileAsync(destinationPath.FullName, overwrite: true, _cancellationTokenSource.Token)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Both IOException (e.g. a destination file locked by another process) and
                    // UnauthorizedAccessException (e.g. denied permissions on the destination) are per-entry
                    // failures. Log against the entry being extracted and continue so one problematic
                    // destination doesn't abort extraction of the rest of the archive.
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
