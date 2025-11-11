// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
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
            StringBuilder buffer = null;

            if (Lines != null && Lines.Length > 0)
            {
                buffer = new StringBuilder(capacity: Lines.Length * 64);

                foreach (ITaskItem line in Lines)
                {
                    buffer.AppendLine(line.ItemSpec);
                }

                contentsAsString = buffer.ToString();
            }
            else
            {
                buffer = new StringBuilder();
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
            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = Directory.GetCurrentDirectory();
            }

            // Create directory if it doesn't exist (common to both transactional and non-transactional modes)
            if (!string.IsNullOrEmpty(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, $"Failed to create directory: {ex.Message}", "");
                    return false;
                }
            }

            // Check WriteOnlyWhenDifferent condition before deciding which execution mode to use
            // This logic is common to both transactional and non-transactional modes
            if (Overwrite && WriteOnlyWhenDifferent)
            {
                MSBuildEventSource.Log.WriteLinesToFileUpToDateStart();
                try
                {
                    if (FileUtilities.FileExistsNoThrow(filePath))
                    {
                        string existingContents = FileSystems.Default.ReadFileAllText(filePath);

                        if (existingContents.Equals(contentsAsString))
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", filePath);
                            MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, true);
                            return true;
                        }
                        else if (FailIfNotIncremental)
                        {
                            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", filePath);
                            MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, true);
                            return false;
                        }
                    }
                }
                catch (IOException)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", filePath);
                }
                MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, false);
            }

            try
            {
                // Use transactional mode by default (enabled via ChangeWave 17.16)
                // Users can opt-out by setting MSBUILDDISABLEFEATURESFROMVERSION=17.16
                if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_16))
                {
                    return ExecuteTransactional(filePath, directoryPath, contentsAsString, encoding);
                }
                else
                {
                    return ExecuteNonTransactional(filePath, directoryPath, contentsAsString, encoding);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Logs a warning when WriteOnlyWhenDifferent is set but Overwrite is false.
        /// This is a shared helper method to ensure consistent behavior between transactional and non-transactional modes.
        /// </summary>
        private void LogWarningIfWriteOnlyWhenDifferentUnused(string filePath)
        {
            if (WriteOnlyWhenDifferent && !Overwrite)
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath);
            }
        }

        private bool ExecuteNonTransactional(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            // Directory creation is already handled in Execute() method
            if (Overwrite)
            {
                // WriteOnlyWhenDifferent check is already done in Execute() method
                System.IO.File.WriteAllText(filePath, contentsAsString, encoding);
            }
            else
            {
                // Log warning when WriteOnlyWhenDifferent is set but Overwrite is false
                LogWarningIfWriteOnlyWhenDifferentUnused(filePath);

                System.IO.File.AppendAllText(filePath, contentsAsString, encoding);
            }

            return !Log.HasLoggedErrors;
        }

        private bool ExecuteTransactional(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            // Implementation inspired by Visual Studio editor pattern: write to temp file, then atomic replace
            // https://github.com/microsoft/vs-editor-api/blob/main/src/Editor/Text/Impl/TextModel/FileUtilities.cs

            if (string.IsNullOrEmpty(filePath))
            {
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, "Target file path is null or empty.", "");
                return false;
            }

            // Directory creation is already handled in Execute() method
            // WriteOnlyWhenDifferent check for Overwrite mode is already done in Execute() method
            // Only handle warning for WriteOnlyWhenDifferent when Overwrite is false
            if (WriteOnlyWhenDifferent && !Overwrite)
            {
                // Log warning when WriteOnlyWhenDifferent is set but Overwrite is false
                LogWarningIfWriteOnlyWhenDifferentUnused(filePath);
            }

            // Generate unique temp file name
            string tempFileName = $"temp_{Guid.NewGuid():N}";
            string tempFile = Path.Combine(directoryPath, tempFileName);

            try
            {
                // Prepare content for temp file following Visual Studio editor pattern
                string tempFileContent = contentsAsString; // Default: use new content only
                
                if (Overwrite)
                {
                    // Overwrite mode: write only new content
                    tempFileContent = contentsAsString;
                }
                else
                {
                    // Append mode: copy existing content first, then append new content
                    if (FileUtilities.FileExistsNoThrow(filePath))
                    {
                        // Retry reading existing content in case file is temporarily locked
                        const int readRetries = 3;
                        int remainingReadAttempts = readRetries;
                        bool readSuccess = false;
                        
                        while (remainingReadAttempts-- > 0 && !readSuccess)
                        {
                            try
                            {
                                string existingContent = System.IO.File.ReadAllText(filePath);
                                tempFileContent = existingContent + contentsAsString;
                                readSuccess = true;
                            }
                            catch (IOException ex)
                            {
                                if (remainingReadAttempts > 0)
                                {
                                    // File might be locked by another process doing atomic replace, retry after short delay
                                    Thread.Sleep(10);
                                }
                                else
                                {
                                    // After all retries failed, fallback to appending only new content
                                    // This prevents build failure while still attempting to preserve data
                                    tempFileContent = contentsAsString; // Fallback: append only new content
                                }
                            }
                        }
                    }
                    // else: file doesn't exist, tempFileContent already set to contentsAsString above
                }

                // Write content to temp file (Visual Studio editor pattern: write to new temp file)
                try
                {
                    System.IO.File.WriteAllText(tempFile, tempFileContent, encoding);
                }
                catch (IOException ex)
                {
                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", tempFile, $"Failed to write to temporary file: {ex.Message}", "");
                    return false;
                }

                // Attempt to replace target file with temporary file (atomic operation)
                // Use exponential backoff to handle high contention scenarios
                const int maxRetries = 20;
                int remainingAttempts = maxRetries;
                int baseDelayMs = 10;
                int currentDelayMs = baseDelayMs;
                while (remainingAttempts-- > 0)
                {
                    try
                    {
                        System.IO.File.Replace(tempFile, filePath, null, true);
                        return !Log.HasLoggedErrors;
                    }
                    catch (FileNotFoundException)
                    {
                        // Target file doesn't exist, try to move temp file to target
                        try
                        {
                            System.IO.File.Move(tempFile, filePath);
                            return !Log.HasLoggedErrors;
                        }
                        catch (IOException moveEx)
                        {
                            // File might have been created by another process, retry replace
                            if (remainingAttempts > 0)
                            {
                                Thread.Sleep(currentDelayMs);
                                // Exponential backoff: increase delay for next retry
                                currentDelayMs = Math.Min(currentDelayMs * 2, 200); // Cap at 200ms
                            }
                            else
                            {
                                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, $"Failed to move temporary file to target: {moveEx.Message}", "");
                                return false;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        // File might be locked, retry after delay with exponential backoff
                        if (remainingAttempts > 0)
                        {
                            Thread.Sleep(currentDelayMs);
                            // Exponential backoff: increase delay for next retry
                            currentDelayMs = Math.Min(currentDelayMs * 2, 200); // Cap at 200ms
                        }
                    }
                }

                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, $"Failed to replace file after {maxRetries} attempts.", "");
                return false;
            }
            catch (Exception ex)
            {
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, $"Unexpected error while processing file: {ex.Message}", "");
                return false;
            }
            finally
            {
                // Clean up temporary file if it exists
                if (FileUtilities.FileExistsNoThrow(tempFile))
                {
                    try
                    {
                        System.IO.File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorDeletingTempFile", tempFile, ex.Message);
                    }
                }
            }
        }
    }
}
