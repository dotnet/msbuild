// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        /// <summary>
        /// File to read lines from.
        /// </summary>
        [Required]
        public ITaskItem File { get; set; }

        /// <summary>
        /// Receives lines from file.
        /// </summary>
        [Output]
        public ITaskItem[] Lines { get; set; } = Array.Empty<ITaskItem>();

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
                    try
                    {
                        string[] textLines = System.IO.File.ReadAllLines(File.ItemSpec);

                        var nonEmptyLines = new List<ITaskItem>();
                        char[] charsToTrim = { '\0', ' ', '\t' };

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

                        Lines = nonEmptyLines.ToArray();
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        Log.LogErrorWithCodeFromResources("ReadLinesFromFile.ErrorOrWarning", File.ItemSpec, e.Message);
                        success = false;
                    }
                }
            }

            return success;
        }
    }
}
