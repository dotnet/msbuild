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
    public class WriteLinesToFile : TaskExtension, IIncrementalTask
    {
        // Default encoding taken from System.IO.WriteAllText()
        private static readonly Encoding s_defaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
            bool success = true;

            if (File == null)
            {
                return success;
            }

            string filePath = FileUtilities.NormalizePath(File.ItemSpec);

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

        private bool ExecuteNonTransactional(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
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
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath);
                    }

                    System.IO.File.AppendAllText(filePath, contentsAsString, encoding);
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        private bool ExecuteTransactional(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
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
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath);
                    }

                    // For append mode, use atomic write to append only the new content
                    // This avoids race conditions from reading-modifying-writing entire file
                    return SaveAtomicallyAppend(filePath, directoryPath, contentsAsString, encoding);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        /// <summary>
        /// Saves content to file atomically using a temporary file, following the Visual Studio editor pattern.
        /// This is for overwrite mode where we write the entire content.
        /// </summary>
        private bool SaveAtomically(string filePath, string contentsAsString, Encoding encoding)
        {
            string temporaryFilePath = null;
            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);

                // Create temporary file with ~ suffix (hides from GIT)
                temporaryFilePath = Path.Combine(directoryPath, Path.GetRandomFileName() + "~");

                // Write content to temporary file
                System.IO.File.WriteAllText(temporaryFilePath, contentsAsString, encoding);

                // Attempt to atomically replace target file with temporary file
                try
                {
                    // Replace the contents of filePath with the contents of the temporary using File.Replace
                    // to preserve the various attributes of the original file.
                    System.IO.File.Replace(temporaryFilePath, filePath, null, true);
                    temporaryFilePath = null; // Mark as successfully replaced
                    return !Log.HasLoggedErrors;
                }
                catch (FileNotFoundException)
                {
                    // The target file doesn't exist, which is fine. Move the temp file to target.
                    try
                    {
                        System.IO.File.Move(temporaryFilePath, filePath);
                        temporaryFilePath = null; // Mark as successfully moved
                        return !Log.HasLoggedErrors;
                    }
                    catch (IOException moveEx)
                    {
                        // Move failed, log and return
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, moveEx.Message, lockedFileMessage);
                        return !Log.HasLoggedErrors;
                    }
                }
                catch (IOException)
                {
                    // Replace failed (likely file is locked). Retry a few times with small delay.
                    for (int retry = 1; retry < 3; retry++)
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(10);
                            System.IO.File.Replace(temporaryFilePath, filePath, null, true);
                            temporaryFilePath = null; // Mark as successfully replaced
                            return !Log.HasLoggedErrors;
                        }
                        catch (IOException)
                        {
                            // Continue to next retry
                        }
                    }

                    // Retries exhausted. Try simple write as fallback.
                    try
                    {
                        System.IO.File.WriteAllText(filePath, contentsAsString, encoding);
                        temporaryFilePath = null; // Mark temp as not needed
                        return !Log.HasLoggedErrors;
                    }
                    catch (Exception fallbackEx) when (ExceptionHandling.IsIoRelatedException(fallbackEx))
                    {
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, fallbackEx.Message, lockedFileMessage);
                        return !Log.HasLoggedErrors;
                    }
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
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
        }

        /// <summary>
        /// Appends content to file atomically. For append mode, we simply append the new content
        /// directly without reading the entire file, avoiding race conditions.
        /// </summary>
        private bool SaveAtomicallyAppend(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
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
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
                return !Log.HasLoggedErrors;
            }
        }

        /// <summary>
        /// Checks if file should be written for Overwrite mode, considering WriteOnlyWhenDifferent option.
        /// </summary>
        /// <returns>True if file should be written, false if write should be skipped.</returns>
        private bool ShouldWriteFileForOverwrite(string filePath, string contentsAsString)
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
                        Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", filePath);
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, true);
                        return false; // Skip write - content is identical
                    }
                    else if (FailIfNotIncremental)
                    {
                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", filePath);
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, false);
                        return false; // Skip write - file differs and FailIfNotIncremental is set
                    }
                }
            }
            catch (IOException)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", filePath);
            }

            MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, false);
            return true; // Proceed with write
        }

        /// <summary>
        /// Compares file contents with the given string using streams to avoid loading the entire file into memory.
        /// Uses the default encoding for the comparison.
        /// </summary>
        /// <returns>True if file contents are identical to the provided string, false otherwise.</returns>
        private bool FilesAreIdentical(string filePath, string contentsAsString)
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
