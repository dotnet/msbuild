// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can extract a .zip archive.
    /// </summary>
    public sealed class Unzip : TaskExtension, ICancelableTask, IIncrementalTask
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
        private string[] _includePatterns;

        /// <summary>
        /// Stores the exclude patterns after parsing.
        /// </summary>
        private string[] _excludePatterns;

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> with a destination folder path to unzip the files to.
        /// </summary>
        [Required]
        public ITaskItem DestinationFolder { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether read-only files should be overwritten.
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether files should be skipped if the destination is unchanged.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem"/> objects containing the paths to .zip archive files to unzip.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        /// <summary>
        /// Gets or sets an MSBuild glob expression that specifies which files to include being unzipped from the archive.
        /// </summary>
        public string Include { get; set; }

        /// <summary>
        /// Gets or sets an MSBuild glob expression that specifies which files to exclude from being unzipped from the archive.
        /// </summary>
        public string Exclude { get; set; }

        public bool FailIfNotIncremental { get; set; }

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
                destinationDirectory = Directory.CreateDirectory(DestinationFolder.ItemSpec);
            }
            catch (Exception e)
            {
                Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotCreateDestinationDirectory", DestinationFolder.ItemSpec, e.Message);

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
                        if (!FileSystems.Default.FileExists(sourceFile.ItemSpec))
                        {
                            Log.LogErrorWithCodeFromResources("Unzip.ErrorFileDoesNotExist", sourceFile.ItemSpec);
                            continue;
                        }

                        try
                        {
                            using (FileStream stream = new FileStream(sourceFile.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0x1000, useAsync: false))
                            {
#pragma warning disable CA2000 // Dispose objects before losing scope because ZipArchive will dispose the stream when it is disposed.
                                using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                                {
                                    try
                                    {
                                        Extract(zipArchive, destinationDirectory);
                                    }
                                    catch (Exception e)
                                    {
                                        // Unhandled exception in Extract() is a bug!
                                        Log.LogErrorFromException(e, showStackTrace: true);
                                        return false;
                                    }
                                }
#pragma warning restore CA2000 // Dispose objects before losing scope
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception e)
                        {
                            // Should only be thrown if the archive could not be opened (Access denied, corrupt file, etc)
                            Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotOpenFile", sourceFile.ItemSpec, e.Message);
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
        /// Extracts all files to the specified directory.
        /// </summary>
        /// <param name="sourceArchive">The <see cref="ZipArchive"/> containing the files to extract.</param>
        /// <param name="destinationDirectory">The <see cref="DirectoryInfo"/> to extract files to.</param>
        private void Extract(ZipArchive sourceArchive, DirectoryInfo destinationDirectory)
        {
            string fullDestinationDirectoryPath = Path.GetFullPath(FileUtilities.EnsureTrailingSlash(destinationDirectory.FullName));

            foreach (ZipArchiveEntry zipArchiveEntry in sourceArchive.Entries.TakeWhile(i => !_cancellationToken.IsCancellationRequested))
            {
                if (ShouldSkipEntry(zipArchiveEntry))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Unzip.DidNotUnzipBecauseOfFilter", zipArchiveEntry.FullName);
                    continue;
                }

                string fullDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectory.FullName, zipArchiveEntry.FullName));
                ErrorUtilities.VerifyThrowInvalidOperation(fullDestinationPath.StartsWith(fullDestinationDirectoryPath, FileUtilities.PathComparison), "Unzip.ZipSlipExploit", fullDestinationPath);

                FileInfo destinationPath = new(fullDestinationPath);

                // Zip archives can have directory entries listed explicitly.
                // If this entry is a directory we should create it and move to the next entry.
                if (Path.GetFileName(destinationPath.FullName).Length == 0)
                {
                    // The entry is a directory
                    Directory.CreateDirectory(destinationPath.FullName);
                    continue;
                }

                if (!destinationPath.FullName.StartsWith(destinationDirectory.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    // ExtractToDirectory() throws an IOException for this but since we're extracting one file at a time
                    // for logging and cancellation, we need to check for it ourselves.
                    Log.LogErrorFromResources("Unzip.ErrorExtractingResultsInFilesOutsideDestination", destinationPath.FullName, destinationDirectory.FullName);
                    continue;
                }

                if (ShouldSkipEntry(zipArchiveEntry, destinationPath))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "Unzip.DidNotUnzipBecauseOfFileMatch", zipArchiveEntry.FullName, destinationPath.FullName, nameof(SkipUnchangedFiles), "true");
                    continue;
                }
                else if (FailIfNotIncremental)
                {
                    Log.LogErrorFromResources("Unzip.FileComment", zipArchiveEntry.FullName, destinationPath.FullName);
                    continue;
                }

                try
                {
                    destinationPath.Directory?.Create();
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotCreateDestinationDirectory", destinationPath.DirectoryName, e.Message);
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
                        Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotMakeFileWriteable", zipArchiveEntry.FullName, destinationPath.FullName, e.Message, lockedFileMessage);
                        continue;
                    }
                }

                try
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "Unzip.FileComment", zipArchiveEntry.FullName, destinationPath.FullName);

#if NET
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
                    // We don't apply UnixFileMode.None because .zip files created on Windows and .zip files created
                    // with previous versions of .NET don't include permissions.
                    UnixFileMode mode = (UnixFileMode)(zipArchiveEntry.ExternalAttributes >> 16) & OwnershipPermissions;
                    if (mode != UnixFileMode.None && !NativeMethodsShared.IsWindows)
                    {
                        fileStreamOptions.UnixCreateMode = mode;
                    }
                    using (FileStream destination = new FileStream(destinationPath.FullName, fileStreamOptions))
#else
                    using (Stream destination = File.Open(destinationPath.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
#endif
                    using (Stream stream = zipArchiveEntry.Open())
                    {
                        stream.CopyToAsync(destination, _DefaultCopyBufferSize, _cancellationToken.Token)
                            .ConfigureAwait(continueOnCapturedContext: false)
                            .GetAwaiter()
                            .GetResult();
                    }

                    destinationPath.LastWriteTimeUtc = zipArchiveEntry.LastWriteTime.UtcDateTime;
                }
                catch (IOException e)
                {
                    Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotExtractFile", zipArchiveEntry.FullName, destinationPath.FullName, e.Message);
                }
            }
        }

        /// <summary>
        /// Determines whether or not a file should be skipped when unzipping by filtering.
        /// </summary>
        /// <param name="zipArchiveEntry">The <see cref="ZipArchiveEntry"/> object containing information about the file in the zip archive.</param>
        /// <returns><code>true</code> if the file should be skipped, otherwise <code>false</code>.</returns>
        private bool ShouldSkipEntry(ZipArchiveEntry zipArchiveEntry)
        {
            bool result = false;

            if (_includePatterns.Length > 0)
            {
                result = _includePatterns.All(pattern => !FileMatcher.IsMatch(FileMatcher.Normalize(zipArchiveEntry.FullName), pattern));
            }

            if (_excludePatterns.Length > 0)
            {
                result |= _excludePatterns.Any(pattern => FileMatcher.IsMatch(FileMatcher.Normalize(zipArchiveEntry.FullName), pattern));
            }

            return result;
        }

        /// <summary>
        /// Determines whether or not a file should be skipped when unzipping.
        /// </summary>
        /// <param name="zipArchiveEntry">The <see cref="ZipArchiveEntry"/> object containing information about the file in the zip archive.</param>
        /// <param name="fileInfo">A <see cref="FileInfo"/> object containing information about the destination file.</param>
        /// <returns><code>true</code> if the file should be skipped, otherwise <code>false</code>.</returns>
        private bool ShouldSkipEntry(ZipArchiveEntry zipArchiveEntry, FileInfo fileInfo)
        {
            return SkipUnchangedFiles
                   && fileInfo.Exists
                   && zipArchiveEntry.LastWriteTime == fileInfo.LastWriteTimeUtc
                   && zipArchiveEntry.Length == fileInfo.Length;
        }

        private void ParseIncludeExclude()
        {
            ParsePattern(Include, out _includePatterns);
            ParsePattern(Exclude, out _excludePatterns);
        }

        private void ParsePattern(string pattern, out string[] patterns)
        {
            patterns = [];
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                if (FileMatcher.HasPropertyOrItemReferences(pattern))
                {
                    // Supporting property references would require access to Expander which is unavailable in Microsoft.Build.Tasks
                    Log.LogErrorWithCodeFromResources("Unzip.ErrorParsingPatternPropertyReferences", pattern);
                }
                else if (pattern.AsSpan().IndexOfAny(FileUtilities.InvalidPathChars) >= 0)
                {
                    Log.LogErrorWithCodeFromResources("Unzip.ErrorParsingPatternInvalidPath", pattern);
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
