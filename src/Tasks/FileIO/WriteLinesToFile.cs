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
        /// If true, use transactional write mode: write to a temp file, rename existing file, rename temp file to target, and delete old file.
        /// Retries renaming if the file is locked.
        /// </summary>
        public bool Transactional { get; set; }

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

            try
            {
                if (Transactional)
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
                success = false;
            }

            return success;
        }

        private bool ExecuteNonTransactional(string filePath, string directoryPath, string contentsAsString, Encoding encoding)
        {
            // Preserve original non-transactional logic exactly
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (Overwrite)
            {
                // When WriteOnlyWhenDifferent is set, read the file and if they're the same return.
                if (WriteOnlyWhenDifferent)
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

            return true;
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

            // Create directory if it doesn't exist
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

            // Handle WriteOnlyWhenDifferent check for transactional mode
            if (WriteOnlyWhenDifferent && Overwrite)
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
                                    // After all retries failed, log warning and fallback to appending only new content
                                    // This prevents build failure while still attempting to preserve data
                                    Log.LogWarningWithCodeFromResources("WriteLinesToFile.ErrorReadingFileTransactional", filePath, ex.Message);
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
                const int maxRetries = 10;
                int remainingAttempts = maxRetries;
                while (remainingAttempts-- > 0)
                {
                    try
                    {
                        System.IO.File.Replace(tempFile, filePath, null, true);
                        return true;
                    }
                    catch (FileNotFoundException)
                    {
                        // Target file doesn't exist, try to move temp file to target
                        try
                        {
                            System.IO.File.Move(tempFile, filePath);
                            return true;
                        }
                        catch (IOException moveEx)
                        {
                            // File might have been created by another process, retry replace
                            if (remainingAttempts > 0)
                            {
                                Thread.Sleep(20);
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
                        // File might be locked, retry after short delay
                        if (remainingAttempts > 0)
                        {
                            Thread.Sleep(20);
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
