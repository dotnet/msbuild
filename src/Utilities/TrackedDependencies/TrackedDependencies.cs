// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class contains utility functions to assist with tracking dependencies
    /// </summary>
    public static class TrackedDependencies
    {
#pragma warning disable format // region formatting is different in net7.0 and net472, and cannot be fixed for both
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
                        files = FileMatcher.Default.GetFiles(null, item.ItemSpec).FileList;
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

            if (files?.Length > 0)
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
#pragma warning restore format
    }
}
