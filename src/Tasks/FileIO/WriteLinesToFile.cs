// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Appends a list of items to a file. One item per line with carriage returns in-between.
    /// </summary>
    public class WriteLinesToFile : TaskExtension
    {
        private ITaskItem _file = null;
        private ITaskItem[] _lines = null;
        private bool _overwrite = false;
        private string _encoding = null;

        // Default encoding taken from System.IO.WriteAllText()
        private static readonly Encoding s_defaultEncoding = new UTF8Encoding(false, true);

        /// <summary>
        /// File to write lines to.
        /// </summary>
        [Required]
        public ITaskItem File
        {
            get { return _file; }
            set { _file = value; }
        }

        /// <summary>
        /// Write each item as a line in the file.
        /// </summary>
        public ITaskItem[] Lines
        {
            get { return _lines; }
            set { _lines = value; }
        }

        /// <summary>
        /// If true, overwrite any existing file contents.
        /// </summary>
        public bool Overwrite
        {
            get { return _overwrite; }
            set { _overwrite = value; }
        }

        /// <summary>
        /// If true, overwrite any existing file contents.
        /// </summary>
        public string Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }

        /// <summary>
        /// If true, the target file specified, if it exists, will be read first to compare against
        /// what the task would have written. If identical, the file is not written to disk and the
        /// timestamp will be preserved.
        /// </summary>
        public bool WriteOnlyWhenDifferent { get; set; }


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
                if (_encoding != null)
                {
                    try
                    {
                        encoding = System.Text.Encoding.GetEncoding(_encoding);
                    }
                    catch (ArgumentException)
                    {
                        Log.LogErrorWithCodeFromResources("General.InvalidValue", "Encoding", "WriteLinesToFile");
                        return false;
                    }
                }

                try
                {
                    if (Overwrite)
                    {
                        if (buffer.Length == 0)
                        {
                            // if overwrite==true, and there are no lines to write,
                            // just delete the file to leave everything tidy.
                            System.IO.File.Delete(File.ItemSpec);
                        }
                        else
                        {
                            string contentsAsString = null;

                            try
                            {
                                // When WriteOnlyWhenDifferent is set, read the file and if they're the same return.
                                if (WriteOnlyWhenDifferent && FileUtilities.FileExistsNoThrow(File.ItemSpec))
                                {
                                    var existingContents = System.IO.File.ReadAllText(File.ItemSpec);
                                    if (existingContents.Length == buffer.Length)
                                    {
                                        contentsAsString = buffer.ToString();
                                        if (existingContents.Equals(contentsAsString))
                                        {
                                            Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.SkippingUnchangedFile", File.ItemSpec);
                                            return true;
                                        }
                                    }
                                }
                            }
                            catch (IOException)
                            {
                                Log.LogMessageFromResources(MessageImportance.Low, "WriteLinesToFile.ErrorReadingFile", File.ItemSpec);
                            }

                            if (contentsAsString == null)
                            {
                                contentsAsString = buffer.ToString();
                            }

                            System.IO.File.WriteAllText(File.ItemSpec, contentsAsString, encoding);
                        }
                    }
                    else
                    {
                        System.IO.File.AppendAllText(File.ItemSpec, buffer.ToString(), encoding);
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    LogError(_file, e, ref success);
                }
            }

            return success;
        }

        /// <summary>
        /// Log an error.
        /// </summary>
        /// <param name="file">The being accessed</param>
        /// <param name="e">The exception.</param>
        /// <param name="success">Whether the task should return an error.</param>
        private void LogError(ITaskItem fileName, Exception e, ref bool success)
        {
            Log.LogErrorWithCodeFromResources("WriteLinesToFile.ErrorOrWarning", fileName.ItemSpec, e.Message);
            success = false;
        }
    }
}
