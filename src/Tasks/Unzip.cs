// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can extract a .zip archive.
    /// </summary>
    public sealed class Unzip : TaskExtension, ICancelableTask
    {
        /// <summary>
        /// Stores a value indicating if a cancellation was requested.
        /// </summary>
        private bool _canceling;

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> with a destination folder path to unzip the files to.
        /// </summary>
        [Required]
        public ITaskItem DestinationFolder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether read-only files should be overwritten.
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether files should be skipped if the destination is unchanged.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets an array of <see cref="ITaskItem"/> objects containing the paths to .zip archive files to unzip.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        /// <inheritdoc cref="ICancelableTask.Cancel"/>
        public void Cancel()
        {
            _canceling = true;
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
                Log.LogErrorFromResources("Unzip.ErrorCouldNotCreateDestinationDirectory", DestinationFolder.ItemSpec, e.Message);

                return false;
            }

            foreach (ITaskItem sourceFile in SourceFiles.TakeWhile(i => !_canceling))
            {
                if (!File.Exists(sourceFile.ItemSpec))
                {
                    Log.LogErrorFromResources("Unzip.ErrorFileDoesNotExist", sourceFile.ItemSpec);
                    continue;
                }

                try
                {
                    using (FileStream stream = new FileStream(sourceFile.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0x1000, useAsync: false))
                    {
                        using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                        {
                            try
                            {
                                Extract(sourceFile, zipArchive, destinationDirectory);
                            }
                            catch (Exception e)
                            {
                                // Unhandled exception in Extract() is a bug!
                                Log.LogErrorFromException(e, showStackTrace: true);
                                return false;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Should only be thrown if the archive could not be opened (Access denied, corrupt file, etc)
                    Log.LogErrorFromResources("Unzip.ErrorCouldNotOpenFile", sourceFile.ItemSpec, e.Message);
                }
            }

            return !_canceling && !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Extracts all files to the specified directory.
        /// </summary>
        /// <param name="sourceTaskItem">The original <see cref="ITaskItem"/> containing details about the source file.</param>
        /// <param name="sourceArchive">The <see cref="ZipArchive"/> containing the files to extract.</param>
        /// <param name="destinationDirectory">The <see cref="DirectoryInfo"/> to extract files to.</param>
        private void Extract(ITaskItem sourceTaskItem, ZipArchive sourceArchive, DirectoryInfo destinationDirectory)
        {
            foreach (ZipArchiveEntry zipArchiveEntry in sourceArchive.Entries.TakeWhile(i => !_canceling))
            {
                FileInfo destinationPath = new FileInfo(Path.Combine(destinationDirectory.FullName, zipArchiveEntry.FullName));

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
                        Log.LogErrorWithCodeFromResources("Unzip.ErrorCouldNotMakeFileWriteable", zipArchiveEntry.FullName, destinationPath.FullName, e.Message);
                        continue;
                    }
                }

                try
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "Unzip.FileComment", zipArchiveEntry.FullName, destinationPath.FullName);

                    using (Stream destination = File.Open(destinationPath.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (Stream stream = zipArchiveEntry.Open())
                    {
                        stream.CopyTo(destination);
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
    }
}
