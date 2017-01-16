// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Read a list of items from a file.
    /// </summary>
    public class ReadLinesFromFile : TaskExtension
    {
        private ITaskItem _file = null;
        private ITaskItem[] _lines = new TaskItem[0];

        /// <summary>
        /// File to read lines from.
        /// </summary>
        [Required]
        public ITaskItem File
        {
            get { return _file; }
            set { _file = value; }
        }

        /// <summary>
        /// Receives lines from file.
        /// </summary>
        [Output]
        public ITaskItem[] Lines
        {
            get { return _lines; }
            set { _lines = value; }
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
                if (System.IO.File.Exists(File.ItemSpec))
                {
                    string[] textLines = null;
                    try
                    {
                        textLines = System.IO.File.ReadAllLines(File.ItemSpec);

                        ArrayList nonEmptyLines = new ArrayList();
                        char[] charsToTrim = new char[] { '\0', ' ', '\t' };

                        foreach (string textLine in textLines)
                        {
                            // A customer has given us a project with a FileList.txt file containing
                            // a line full of '\0' characters.  We don't know how these characters
                            // got in there, but when we try to read the file back in, we fail
                            // miserably.  Here, we Trim to protect us from this situation.
                            string trimmedTextLine = textLine.Trim(charsToTrim);
                            if (trimmedTextLine.Length > 0)
                            {
                                // The lines were written to the file in unescaped form, so we need to escape them
                                // before passing them to the TaskItem. 
                                nonEmptyLines.Add(new TaskItem(EscapingUtilities.Escape(trimmedTextLine)));
                            }
                        }

                        Lines = (ITaskItem[])nonEmptyLines.ToArray(typeof(ITaskItem));
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        LogError(_file, e, ref success);
                    }
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
            Log.LogErrorWithCodeFromResources("ReadLinesFromFile.ErrorOrWarning", fileName.ItemSpec, e.Message);
            success = false;
        }
    }
}
