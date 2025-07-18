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

        // Maximum number of retries for transactional write
        private const int MaxRetries = 5;

        // Delay between retries in milliseconds
        private const int RetryDelayMs = 100;

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
                    Directory.CreateDirectory(directoryPath);
                    string contentsAsString = buffer.ToString();
                    string targetFile = File.ItemSpec;
                    if (Overwrite)
                    {

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

                        if (Transactional)
                        {
                            string tempFile = Path.Combine(directoryPath, $"temp_{Guid.NewGuid().ToString()}");
                            string backupFile = targetFile + ".bak";

                            // Write to temp file
                            System.IO.File.WriteAllText(tempFile, contentsAsString, encoding);

                            bool renameSucceeded = false;

                            for (int attempt = 0; attempt <= MaxRetries; attempt++)
                            {
                                try
                                {
                                    // If target file exists, rename it to backup
                                    if (FileUtilities.FileExistsNoThrow(targetFile))
                                    {
                                        System.IO.File.Move(targetFile, backupFile);
                                    }

                                    // Rename temp file to target
                                    System.IO.File.Move(tempFile, targetFile);
                                    renameSucceeded = true;

                                    // Delete backup file if it exists
                                    if (FileUtilities.FileExistsNoThrow(backupFile))
                                    {
                                        System.IO.File.Delete(backupFile);
                                    }
                                    break;
                                }
                                catch (IOException) when (attempt < MaxRetries)
                                {
                                    // Wait before retrying
                                    System.Threading.Thread.Sleep(RetryDelayMs);
                                }
                            }
                            if (!renameSucceeded)
                            {
                                // Clean up temp file on failure
                                if (FileUtilities.FileExistsNoThrow(tempFile))
                                {
                                    System.IO.File.Delete(tempFile);
                                }
                                string lockedFileMessage = LockCheck.GetLockedFileMessage(targetFile);
                                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", targetFile, "Failed to rename temporary file after retries.", lockedFileMessage);
                                return false;
                            }
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
