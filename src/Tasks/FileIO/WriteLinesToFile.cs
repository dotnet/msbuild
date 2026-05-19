// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Appends a list of items to a file. One item per line with carriage returns in-between.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class WriteLinesToFile : TaskExtension, IIncrementalTask, IMultiThreadableTask
    {
        // Default encoding taken from System.IO.WriteAllText()
        private static readonly Encoding s_defaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Moves <paramref name="source"/> to <paramref name="destination"/>, overwriting it if it exists.
        /// Uses <c>Microsoft.IO.File</c> on .NET Framework to access the overwrite overload,
        /// which is available natively in <c>System.IO.File</c> on .NET 6+.
        /// </summary>
        private static void MoveFileWithOverwrite(string source, string destination)
        {
#if NETFRAMEWORK
            // Microsoft.IO.Redist backports File.Move(overwrite) to .NET Framework.
            Microsoft.IO.File.Move(source, destination, overwrite: true);
#elif NET
            // File.Move(overwrite) is available natively on .NET 5+.
            System.IO.File.Move(source, destination, overwrite: true);
#else
            // netstandard2.0 output is ref asm only and never executed at runtime.
            throw new PlatformNotSupportedException();
#endif
        }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        /// <summary>
        /// File to write lines to.
        /// </summary>
        [Required]
        public ITaskItem File { get; set; }

        /// <summary>
        /// Write each item as a line in the file.
        /// </summary>
        public ITaskItem[] Lines { get; set; }

        /// <summary>
        /// If true, overwrite any existing file contents.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Encoding to be used.
        /// </summary>
        public string Encoding { get; set; }

        /// <summary>
        /// If true, the target file specified, if it exists, will be read first to compare against
        /// what the task would have written. If identical, the file is not written to disk and the
        /// timestamp will be preserved.
        /// </summary>
        public bool WriteOnlyWhenDifferent { get; set; }

        /// <summary>
        /// Question whether this task is incremental.
        /// </summary>
        /// <remarks>When question is true, then error out if WriteOnlyWhenDifferent would have
        /// written to the file.</remarks>
        public bool FailIfNotIncremental { get; set; }

        [Obsolete]
        public bool CanBeIncremental => WriteOnlyWhenDifferent;

        /// <inheritdoc cref="ITask.Execute" />
        public override bool Execute()
        {
            if (File == null)
            {
                return true;
            }

            ErrorUtilities.VerifyThrowArgumentLength(File.ItemSpec);
            AbsolutePath filePath = FileUtilities.NormalizePath(TaskEnvironment.GetAbsolutePath(File.ItemSpec));
            string contentsAsString = string.Empty;

            if (Lines != null && Lines.Length > 0)
            {
                StringBuilder buffer = new StringBuilder(capacity: Lines.Length * 64);

                foreach (ITaskItem line in Lines)
                {
                    buffer.AppendLine(line.ItemSpec);
                }

                contentsAsString = buffer.ToString();
            }

            Encoding encoding = s_defaultEncoding;
            if (Encoding != null)
            {
                try
                {
                    encoding = System.Text.Encoding.GetEncoding(Encoding);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidValue", "Encoding", "WriteLinesToFile");
                    return false;
                }
            }

            string directoryPath = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directoryPath);

            // Handle WriteOnlyWhenDifferent check for Overwrite mode before executing
            if (Overwrite && WriteOnlyWhenDifferent)
            {
                if (!ShouldWriteFileForOverwrite(filePath, contentsAsString))
                {
                    return !Log.HasLoggedErrors;
                }
            }

            // Use transactional mode by default when ChangeWave 18.3 is enabled
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_3))
            {
                return ExecuteTransactional(filePath, directoryPath, contentsAsString, encoding);
            }
            else
            {
                return ExecuteNonTransactional(filePath, directoryPath, contentsAsString, encoding);
            }
        }

        private bool ExecuteNonTransactional(AbsolutePath filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            try
            {
                if (Overwrite)
                {
                    System.IO.File.WriteAllText(filePath, contentsAsString, encoding);
                }
                else
                {
                    if (WriteOnlyWhenDifferent)
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath.OriginalValue);
                    }

                    System.IO.File.AppendAllText(filePath, contentsAsString, encoding);
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath.OriginalValue, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        private bool ExecuteTransactional(AbsolutePath filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            try
            {
                if (Overwrite)
                {
                    return SaveAtomically(filePath, contentsAsString, encoding);
                }
                else
                {
                    if (WriteOnlyWhenDifferent)
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath.OriginalValue);
                    }

                    // For append mode, use atomic write to append only the new content
                    // This avoids race conditions from reading-modifying-writing entire file
                    return SaveAtomicallyAppend(filePath, directoryPath, contentsAsString, encoding);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath.OriginalValue, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        /// <summary>
        /// Saves content to file atomically using a temporary file.
        /// Writes to a temp file first, then moves it to the target with overwrite,
        /// which handles both the "target exists" and "target doesn't exist" cases in a
        /// single call and eliminates the race window present in a check-then-act pattern.
        /// </summary>
        private bool SaveAtomically(AbsolutePath filePath, string contentsAsString, Encoding encoding)
        {
            string temporaryFilePath = null;
            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);

                // Create temporary file with ~ suffix (hides from GIT)
                temporaryFilePath = Path.Combine(directoryPath, Path.GetRandomFileName() + "~");

                // Write content to temporary file
                System.IO.File.WriteAllText(temporaryFilePath, contentsAsString, encoding);

                // Atomically move temp file to target, overwriting if it already exists.
                // Using overwrite: true handles concurrent writes without a race condition —
                // both "target doesn't exist" and "target already exists" cases are covered
                // by a single operation, with no window between them.
                const int maxAttempts = 3;
                Exception lastException = null;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    if (attempt > 1)
                    {
                        // Log the retry so concurrency issues are visible when diagnosing builds.
                        Log.LogMessageFromResources(MessageImportance.High, "WriteLinesToFile.Retry",
                            filePath.OriginalValue, attempt, maxAttempts, lastException.Message);
                        System.Threading.Thread.Sleep(10);
                    }

                    try
                    {
                        MoveFileWithOverwrite(temporaryFilePath, filePath);
                        temporaryFilePath = null;
                        return !Log.HasLoggedErrors;
                    }
                    catch (Exception ex) when (attempt < maxAttempts && ExceptionHandling.IsIoRelatedException(ex))
                    {
                        lastException = ex;
                    }
                    // On the last attempt, the exception propagates to the outer handler.
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath.OriginalValue, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
            finally
            {
                // Clean up temporary file if it still exists
                if (temporaryFilePath != null)
                {
                    try
                    {
                        if (System.IO.File.Exists(temporaryFilePath))
                        {
                            System.IO.File.Delete(temporaryFilePath);
                        }
                    }
                    catch
                    {
                        // Failing to clean up the temporary is an ignorable exception.
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Appends content to file atomically. For append mode, we simply append the new content
        /// directly without reading the entire file, avoiding race conditions.
        /// </summary>
        private bool SaveAtomicallyAppend(AbsolutePath filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            try
            {
                // For append mode, directly append new content to the file.
                // This avoids the race condition of reading-modify-write entire file.
                // Multiple processes can safely append without losing data.
                System.IO.File.AppendAllText(filePath, contentsAsString, encoding);
                return !Log.HasLoggedErrors;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath.OriginalValue, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        /// <summary>
        /// Checks if file should be written for Overwrite mode, considering WriteOnlyWhenDifferent option.
        /// </summary>
        /// <returns>True if file should be written, false if write should be skipped.</returns>
        private bool ShouldWriteFileForOverwrite(AbsolutePath filePath, string contentsAsString)
        {
            if (!WriteOnlyWhenDifferent)
            {
                return true; // Always write if WriteOnlyWhenDifferent is false
            }

            MSBuildEventSource.Log.WriteLinesToFileUpToDateStart();
            try
            {
                if (FileUtilities.FileExistsNoThrow(filePath))
                {
                    // Use stream-based comparison to avoid loading entire file into memory
                    if (FilesAreIdentical(filePath, contentsAsString))
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", filePath.OriginalValue);
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath.OriginalValue, true);
                        return false; // Skip write - content is identical
                    }
                    else if (FailIfNotIncremental)
                    {
                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", filePath.OriginalValue);
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath.OriginalValue, false);
                        return false; // Skip write - file differs and FailIfNotIncremental is set
                    }
                }
            }
            catch (IOException)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", filePath.OriginalValue);
            }

            MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath.OriginalValue, false);
            return true; // Proceed with write
        }

        /// <summary>
        /// Compares file contents with the given string using streams to avoid loading the entire file into memory.
        /// Uses the default encoding for the comparison.
        /// </summary>
        /// <returns>True if file contents are identical to the provided string, false otherwise.</returns>
        private bool FilesAreIdentical(AbsolutePath filePath, string contentsAsString)
        {
            try
            {
                byte[] newContentBytes = s_defaultEncoding.GetBytes(contentsAsString);

                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096))
                {
                    // Quick check: file size must match
                    if (fileStream.Length != newContentBytes.Length)
                    {
                        return false;
                    }

                    // Compare bytes in chunks to avoid loading entire file into memory
                    byte[] fileBuffer = new byte[4096];
                    int newContentOffset = 0;

                    int bytesRead;
                    while ((bytesRead = fileStream.Read(fileBuffer, 0, fileBuffer.Length)) > 0)
                    {
                        // Compare current chunk with the corresponding part of new content
                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (fileBuffer[i] != newContentBytes[newContentOffset + i])
                            {
                                return false; // Difference found, files are not identical
                            }
                        }

                        newContentOffset += bytesRead;
                    }

                    // All bytes matched
                    return true;
                }
            }
            catch (Exception)
            {
                // If we can't read the file, treat it as different so write proceeds
                return false;
            }
        }
    }
}
