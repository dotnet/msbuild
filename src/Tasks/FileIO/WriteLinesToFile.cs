// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
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
    /// Helper class to manage a global Mutex for file access synchronization.
    /// </summary>
    internal class SingleGlobalInstance : IDisposable
    {
        public bool HasHandle = false;
        private Mutex mutex;

        public SingleGlobalInstance(string mutexName, int millisecondsTimeout = -1)
        {
            try
            {
                mutex = new Mutex(false, mutexName);
                HasHandle = mutex.WaitOne(millisecondsTimeout);
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating mutex {mutexName}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (mutex != null)
            {
                if (HasHandle)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error releasing mutex: {ex.Message}");
                    }
                }
                mutex.Dispose();
                mutex = null;
            }
        }
    }

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

        public bool HasHandle = false;

        // Maximum number of retries for transactional write
        private const int MaxRetries = 5;


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

            if (File != null)
            {
                // do not return if Lines is null, because we may
                // want to delete the file in that case
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

                try
                {
                    var directoryPath = Path.GetDirectoryName(FileUtilities.NormalizePath(File.ItemSpec));
                    string contentsAsString = buffer.ToString();

                    string targetFile = File.ItemSpec;

                    if (Transactional)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.UsingTransactionalMode", targetFile);

                        string mutexName;
                        if (string.IsNullOrEmpty(targetFile))
                        {
                            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, "Target file path is null or empty.", "");
                            return false;
                        }

                        // Create a unique mutex name based on the target file path
                        mutexName = "WriteLinesToFile_" + targetFile.ToLowerInvariant()
                            .Replace(@"\", "_")
                            .Replace(":", "_")
                            .Replace("/", "_")
                            .Replace("?", "_")
                            .Replace("*", "_")
                            .Replace("<", "_")
                            .Replace(">", "_")
                            .Replace("|", "_");

                        // Retry acquiring mutex up to 3 times with 10-second delay
                        int mutexRetries = 3;
                        int mutexRetryDelayMs = 10000;
                        bool acquiredMutex = false;

                        for (int i = 0; i < mutexRetries && !acquiredMutex; i++)
                        {
                            // Attempt to acquire mutex with a 60-second timeout
                            using (var mutexInstance = new SingleGlobalInstance(mutexName, 10000))
                            {
                                if (mutexInstance.HasHandle)
                                {
                                    acquiredMutex = true;
                                    // Generate unique temporary file name
                                    string tempFile = Path.Combine(directoryPath, $"temp_{Guid.NewGuid():N}");
                                    try
                                    {
                                        // Copy existing file to temp file if it exists
                                        if (FileUtilities.FileExistsNoThrow(targetFile))
                                        {
                                            try
                                            {
                                                System.IO.File.Copy(targetFile, tempFile);
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
                                        int remainingAttempts = MaxRetries;
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
                                                Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.Retry", targetFile, MaxRetries - remainingAttempts, MaxRetries, ex.Message);
                                                Thread.Sleep(5);
                                            }
                                        }

                                        Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, $"Failed to replace file after {MaxRetries} attempts.", "");
                                        return false;
                                    }
                                    catch (Exception ex)
                                    {
                                        // Catch any unexpected errors during file operations
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
                    else if (Overwrite)
                    {
                        Directory.CreateDirectory(directoryPath);

                        // When WriteOnlyWhenDifferent is set, read the file and if they're the same return.
                        if (WriteOnlyWhenDifferent)
                        {
                            MSBuildEventSource.Log.WriteLinesToFileUpToDateStart();
                            try
                            {
                                if (FileUtilities.FileExistsNoThrow(File.ItemSpec))
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
                                Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", File.ItemSpec);
                            }
                            MSBuildEventSource.Log.WriteLinesToFileUpToDateStop(File.ItemSpec, false);
                        }

                        System.IO.File.WriteAllText(File.ItemSpec, contentsAsString, encoding);
                    }
                    else
                    {
                        if (WriteOnlyWhenDifferent)
                        {
                            Log.LogMessageFromResources(MessageImportance.Normal, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", File.ItemSpec);
                        }

                        Directory.CreateDirectory(directoryPath);
                        System.IO.File.AppendAllText(File.ItemSpec, buffer.ToString(), encoding);
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    string lockedFileMessage = LockCheck.GetLockedFileMessage(File.ItemSpec);
                    Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", File.ItemSpec, e.Message, lockedFileMessage);
                    success = false;
                }
            }

            return success;
        }
    }
}
