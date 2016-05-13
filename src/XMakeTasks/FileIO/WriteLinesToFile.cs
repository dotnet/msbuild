// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Globalization;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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

                Encoding encode = null;
                if (_encoding != null)
                {
                    try
                    {
                        encode = System.Text.Encoding.GetEncoding(_encoding);
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
                            // Passing a null encoding, or Encoding.Default, to WriteAllText or AppendAllText
                            // is not the same as calling the overload that does not take encoding!
                            // Encoding.Default is based on the current codepage, the overload without encoding is UTF8-without-BOM.
                            if (encode == null)
                            {
                                System.IO.File.WriteAllText(File.ItemSpec, buffer.ToString());
                            }
                            else
                            {
                                System.IO.File.WriteAllText(File.ItemSpec, buffer.ToString(), encode);
                            }
                        }
                    }
                    else
                    {
                        if (encode == null)
                        {
                            System.IO.File.AppendAllText(File.ItemSpec, buffer.ToString());
                        }
                        else
                        {
                            System.IO.File.AppendAllText(File.ItemSpec, buffer.ToString(), encode);
                        }
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
