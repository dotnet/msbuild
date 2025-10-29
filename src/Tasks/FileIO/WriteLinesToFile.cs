// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
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
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string lockedFileMessage = LockCheck.GetLockedFileMessage(filePath);
                Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", filePath, e.Message, lockedFileMessage);
                success = false;
            }

            return success;
        }
    }
}
