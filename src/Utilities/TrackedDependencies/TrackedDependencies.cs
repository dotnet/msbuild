// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class contains utility functions to assist with tracking dependencies
    /// </summary>
    public static class TrackedDependencies
    {
        #region Methods
        /// <summary>
        /// Expand wildcards in the item list.
        /// </summary>
        /// <param name="expand"></param>
        /// <returns>Array of items expanded</returns>
        public static ITaskItem[] ExpandWildcards(ITaskItem[] expand)
        {
            if (expand == null)
            {
                return null;
            }
            else
            {
                List<ITaskItem> expanded = new List<ITaskItem>(expand.Length);
                foreach (ITaskItem i in expand)
                {
                    if (FileMatcher.HasWildcards(i.ItemSpec))
                    {
                        string[] files;
                        string directoryName = Path.GetDirectoryName(i.ItemSpec);
                        string searchPattern = Path.GetFileName(i.ItemSpec);

                        // Very often with TLog files we're talking about
                        // a directory and a simply wildcarded filename
                        // Optimize for that case here.
                        if (!FileMatcher.HasWildcards(directoryName) && Directory.Exists(directoryName))
                        {
                            files = Directory.GetFiles(directoryName, searchPattern);
                        }
                        else
                        {
                            files = FileMatcher.Default.GetFiles(null, i.ItemSpec);
                        }

                        foreach (string file in files)
                        {
                            TaskItem newItem = new TaskItem((ITaskItem)i);
                            newItem.ItemSpec = file;
                            expanded.Add(newItem);
                        }
                    }
                    else
                    {
                        expanded.Add(i);
                    }
                }
                return expanded.ToArray();
            }
        }

        /// <summary>
        /// This method checks that all the files exist
        /// </summary>
        /// <param name="files"></param>
        /// <returns>bool</returns>
        internal static bool ItemsExist(ITaskItem[] files)
        {
            bool allExist = true;

            if (files != null && files.Length > 0)
            {
                foreach (ITaskItem item in files)
                {
                    if (!FileUtilities.FileExistsNoThrow(item.ItemSpec))
                    {
                        allExist = false;
                        break;
                    }
                }
            }
            else
            {
                allExist = false;
            }
            return allExist;
        }
        #endregion
    }
}
