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
using Microsoft.NET.StringTools;

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
            if (File == null)
            {
                return true; // Nothing to do if no file is specified
            }

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
                // Handle WriteOnlyWhenDifferent check in parent function
                if (WriteOnlyWhenDifferent)
                {
                    if (!Overwrite)
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", targetFile);
                    }
                    else
                    {
                        // Read existing content only when needed for WriteOnlyWhenDifferent
                        string existingContents = null;
                        if (FileUtilities.FileExistsNoThrow(targetFile))
                        {
                            try
                            {
                                existingContents = FileSystems.Default.ReadFileAllText(targetFile);
                            }
                            catch (IOException)
                            {
                                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", targetFile);
                            }
                        }

                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStart();
                        if (existingContents != null && existingContents.Length == buffer.Length)
                        {
                            if (existingContents.Equals(contentsAsString))
                            {
                                Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", targetFile);
                                MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(targetFile, true);
                                return !Log.HasLoggedErrors;
                            }
                            else if (FailIfNotIncremental)
                            {
                                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFile", targetFile);
                                MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(targetFile, true);
                                return false;
                            }
                        }
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(targetFile, false);
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
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, e.Message, targetFile);
            }

            return !Log.HasLoggedErrors;
        }

        private bool ExecuteTransactional(string targetFile, string directoryPath, string contentsAsString, Encoding encoding)
        {
            // Implementation inspired by FileUtilities.cs[](https://github.com/microsoft/vs-editor-api/blob/main/src/Editor/Text/Impl/TextModel/FileUtilities.cs)

            if (string.IsNullOrEmpty(targetFile))
            {
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, "Target file path is null or empty.", "");
                return false;
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(directoryPath);

            // Use hash for mutex name to avoid excessive string allocation 
            string normalizedTargetPath = targetFile.ToLowerInvariant();
            int stableHash = FowlerNollVo1aHash.ComputeHash32Fast(normalizedTargetPath);
            string tempFileName = $"temp_{stableHash}_{Guid.NewGuid():N}";
            string tempFile = Path.Combine(directoryPath, tempFileName);
            string mutexName = $"MSBuild_WriteLinesToFile_{stableHash}";

            // Retry acquiring mutex up to 5 times with 200ms delay
            const int mutexRetries = 5;
            const int mutexRetryDelayMs = 200;
            bool acquiredMutex = false;

            for (int i = 0; i < mutexRetries && !acquiredMutex; i++)
            {
                using (var mutex = SystemWideMutex.OpenOrCreateMutex(mutexName, mutexRetryDelayMs))
                {
                    if (mutex.HasHandle)
                    {
                        acquiredMutex = true;
                        try
                        {

                            // Prepare content for temp file following Visual Studio editor pattern
                            string tempFileContent;
                            if (Overwrite)
                            {
                                // Overwrite mode: write only new content
                                tempFileContent = contentsAsString;
                            }
                            else
                            {
                                // Append mode: copy existing content first, then append new content
                                if (FileUtilities.FileExistsNoThrow(targetFile))
                                {
                                    try
                                    {
                                        string existingContent = System.IO.File.ReadAllText(targetFile);
                                        tempFileContent = existingContent + contentsAsString;
                                    }
                                    catch (IOException ex)
                                    {
                                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorReadingFileTransactional", targetFile, ex.Message);
                                        return false;
                                    }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

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
                                else
                                {
                                    tempFileContent = contentsAsString;
                                }
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

                            // Attempt to replace target file with temporary file
                            const int maxRetries = 5;
                            int remainingAttempts = maxRetries;
                            while (remainingAttempts-- > 0)
                            {
                                try
                                {
                                    System.IO.File.Replace(tempFile, targetFile, null, true);
                                    return !Log.HasLoggedErrors;
                                }
                                catch (FileNotFoundException)
                                {
                                    System.IO.File.Move(tempFile, targetFile);
                                    return !Log.HasLoggedErrors;
                                }
                                catch (IOException)
                                {
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
                                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorDeletingTempFile", tempFile, ex.Message);
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(mutexRetryDelayMs);
                        }
                        catch (IOException)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", filePath);
                        }
                        MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(filePath, false);
                    }

                    System.IO.File.WriteAllText(filePath, contentsAsString, encoding);
                }
            }

            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, $"Failed to acquire mutex after {mutexRetries} attempts.", "");
            return false;
        }

        private bool ExecuteNonTransactional(string targetFile, string directoryPath, StringBuilder buffer, Encoding encoding)
        {
            try
            {
                if (Overwrite)
                {
                    Directory.CreateDirectory(directoryPath);
                    string contentsAsString = buffer.ToString();
                    System.IO.File.WriteAllText(targetFile, contentsAsString, encoding);
                }
                else
                {
                    Directory.CreateDirectory(directoryPath);
                    System.IO.File.AppendAllText(targetFile, buffer.ToString(), encoding);
                else
                {
                    if (WriteOnlyWhenDifferent)
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", filePath);
                    }

                    System.IO.File.AppendAllText(filePath, contentsAsString, encoding);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", File.ItemSpec, e.Message, "");
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
                success = false;
                return success;
            }

            return !Log.HasLoggedErrors;
        }
    }
}
