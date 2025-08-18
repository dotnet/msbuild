// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool success = true;

            if (File == null)
            {
                return true; // Nothing to do if no file is specified
            }

            // Prepare the content to write
            StringBuilder buffer = new StringBuilder();
            if (Lines != null)
            {
                foreach (ITaskItem line in Lines)
                {
                    buffer.AppendLine(line.ItemSpec);
                }
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

            string targetFile = File.ItemSpec;
            string contentsAsString = buffer.ToString();
            string directoryPath = Path.GetDirectoryName(FileUtilities.NormalizePath(targetFile));

            try
            {
                if (Transactional)
                {
                    return ExecuteTransactional(targetFile, directoryPath, contentsAsString, encoding);
                }
                else
                {
                    return ExecuteNonTransactional(targetFile, directoryPath, buffer, encoding);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(targetFile);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, e.Message, lockedFileMessage);
                success = false;
            }

            return success;
        }

        private bool ExecuteTransactional(string targetFile, string directoryPath, string contentsAsString, Encoding encoding)
        {
            // Implementation inspired by FileUtilities.cs[](https://github.com/microsoft/vs-editor-api/blob/main/src/Editor/Text/Impl/TextModel/FileUtilities.cs)
            Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.UsingTransactionalMode", targetFile);

            if (string.IsNullOrEmpty(targetFile))
            {
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, "Target file path is null or empty.", "");
                return false;
            }

            // Use hash for mutex name to avoid excessive string allocation
            string tempFileName = $"temp_{targetFile.GetHashCode()}";
            string tempFile = Path.Combine(directoryPath, tempFileName);
            string mutexName = $"WriteLinesToFile_{tempFileName}";

            // Retry acquiring mutex up to 5 times with 200ms delay
            const int mutexRetries = 5;
            const int mutexRetryDelayMs = 200;
            bool acquiredMutex = false;

            for (int i = 0; i < mutexRetries && !acquiredMutex; i++)
            {
                using (var mutex = FileAccessMutex.OpenOrCreateMutex(mutexName, mutexRetryDelayMs))
                {
                    if (mutex.IsLocked)
                    {
                        acquiredMutex = true;
                        try
                        {
                            // Copy existing file to temp file if it exists
                            if (FileUtilities.FileExistsNoThrow(targetFile))
                            {
                                try
                                {
                                    System.IO.File.Copy(targetFile, tempFile, true);
                                }
                                catch (IOException ex)
                                {
                                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFileTransactional", targetFile, ex.Message);
                                    return false;
                                }
                            }

                            // Append new content to temp file
                            try
                            {
                                System.IO.File.AppendAllText(tempFile, contentsAsString, encoding);
                            }
                            catch (IOException ex)
                            {
                                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", tempFile, $"Failed to append to temporary file: {ex.Message}", "");
                                return false;
                            }

                            // Attempt to replace target file with temporary file
                            const int maxRetries = 5;
                            int remainingAttempts = maxRetries;
                            while (remainingAttempts-- > 0)
                            {
                                try
                                {
                                    System.IO.File.Replace(tempFile, targetFile, null, true);
                                    return true;
                                }
                                catch (FileNotFoundException)
                                {
                                    System.IO.File.Move(tempFile, targetFile);
                                    return true;
                                }
                                catch (IOException ex)
                                {
                                    Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.Retry", targetFile, maxRetries - remainingAttempts, maxRetries, ex.Message);
                                    Thread.Sleep(5);
                                }
                            }

                            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, $"Failed to replace file after {maxRetries} attempts.", "");
                            return false;
                        }
                        catch (Exception ex)
                        {
                            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, $"Unexpected error while processing file: {ex.Message}", "");
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
                                    Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorDeletingTempFile", tempFile, ex.Message);
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.Retry", targetFile, i + 1, mutexRetries, "Failed to acquire mutex.");
                        Thread.Sleep(mutexRetryDelayMs);
                    }
                }
            }

            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, $"Failed to acquire mutex after {mutexRetries} attempts.", "");
            return false;
        }

        private bool ExecuteNonTransactional(string targetFile, string directoryPath, StringBuilder buffer, Encoding encoding)
        {
            if (Overwrite)
            {
                Directory.CreateDirectory(directoryPath);
                string contentsAsString = buffer.ToString();

                // When WriteOnlyWhenDifferent is set, read the file and if they're the same return.
                if (WriteOnlyWhenDifferent)
                {
                    MSBuildEventSource.Log.WriteLinesToFileUpToDateStart();
                    try
                    {
                        if (FileUtilities.FileExistsNoThrow(targetFile))
                        {
                            string existingContents = System.IO.File.ReadAllText(File.ItemSpec);
                            if (existingContents.Length == buffer.Length)
                            {
                                if (existingContents.Equals(contentsAsString))
                                {
                                    Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", File.ItemSpec);
                                    MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(File.ItemSpec, true);
                                    return true;
                                }
                                else if (FailIfNotIncremental)
                                {
                                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", File.ItemSpec);
                                    return false;
                                }
                            }
                        }
                    }
                    catch (IOException)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", targetFile);
                    }
                    MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(targetFile, false);
                }

                System.IO.File.WriteAllText(targetFile, contentsAsString, encoding);
            }
            else
            {
                if (WriteOnlyWhenDifferent)
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", targetFile);
                }

                System.IO.File.AppendAllText(targetFile, buffer.ToString(), encoding);
            }

            return true;
        }
    }
}
