// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

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

            var expanded = new List<ITaskItem>(expand.Length);
            foreach (ITaskItem item in expand)
            {
                if (FileMatcher.HasWildcards(item.ItemSpec))
                {
                    string[] files;
                    string directoryName = Path.GetDirectoryName(item.ItemSpec);
                    string searchPattern = Path.GetFileName(item.ItemSpec);

                    // Very often with TLog files we're talking about
                    // a directory and a simply wildcarded filename
                    // Optimize for that case here.
                    if (!FileMatcher.HasWildcards(directoryName) && FileSystems.Default.DirectoryExists(directoryName))
                    {
                        files = Directory.GetFiles(directoryName, searchPattern);
                    }
                    else
                    {
                        files = FileMatcher.Default.GetFiles(null, item.ItemSpec);
                    }

                    foreach (string file in files)
                    {
                        expanded.Add(new TaskItem(item) { ItemSpec = file });
                    }
                }
                else
                {
                    expanded.Add(item);
                }
            }
            return expanded.ToArray();
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
