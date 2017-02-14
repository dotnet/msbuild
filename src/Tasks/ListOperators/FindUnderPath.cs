// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Given a list of items, determine which are in the cone of the folder passed in and which aren't.
    /// </summary>
    public class FindUnderPath : TaskExtension
    {
        private bool _updateToAbsolutePaths = false;
        private ITaskItem _path = null;
        private ITaskItem[] _files = new TaskItem[0];
        private ITaskItem[] _inPath = null;
        private ITaskItem[] _outOfPath = null;

        /// <summary>
        /// Filter based on whether items fall under this path or not.
        /// </summary>
        [Required]
        public ITaskItem Path
        {
            get { return _path; }
            set { _path = value; }
        }

        /// <summary>
        /// Files to consider.
        /// </summary>
        public ITaskItem[] Files
        {
            get { return _files; }
            set { _files = value; }
        }

        /// <summary>
        /// Set to true if the paths of the output items should be updated to be absolute
        /// </summary>
        public bool UpdateToAbsolutePaths
        {
            get { return _updateToAbsolutePaths; }
            set { _updateToAbsolutePaths = value; }
        }

        /// <summary>
        /// Files that were inside of Path.
        /// </summary>
        [Output]
        public ITaskItem[] InPath
        {
            get { return _inPath; }
            set { _inPath = value; }
        }

        /// <summary>
        /// Files that were outside of Path.
        /// </summary>
        [Output]
        public ITaskItem[] OutOfPath
        {
            get { return _outOfPath; }
            set { _outOfPath = value; }
        }

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            ArrayList inPathList = new ArrayList();
            ArrayList outOfPathList = new ArrayList();

            string conePath;

            try
            {
                conePath =
                    OpportunisticIntern.InternStringIfPossible(
                        System.IO.Path.GetFullPath(FileUtilities.FixFilePath(_path.ItemSpec)));
                conePath = FileUtilities.EnsureTrailingSlash(conePath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources(null, "", 0, 0, 0, 0,
                    "FindUnderPath.InvalidParameter", "Path", _path.ItemSpec, e.Message);
                return false;
            }

            int conePathLength = conePath.Length;

            Log.LogMessageFromResources(MessageImportance.Low, "FindUnderPath.ComparisonPath", Path.ItemSpec);

            foreach (ITaskItem item in Files)
            {
                string fullPath;
                try
                {
                    fullPath =
                        OpportunisticIntern.InternStringIfPossible(
                            System.IO.Path.GetFullPath(FileUtilities.FixFilePath(item.ItemSpec)));
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    Log.LogErrorWithCodeFromResources(null, "", 0, 0, 0, 0,
                        "FindUnderPath.InvalidParameter", "Files", item.ItemSpec, e.Message);
                    return false;
                }

                // Compare the left side of both strings to see if they're equal.
                if (String.Compare(conePath, 0, fullPath, 0, conePathLength, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // If we should use the absolute path, update the item contents
                    // Since ItemSpec, which fullPath comes from, is unescaped, re-escape when setting
                    // item.ItemSpec, since the setter for ItemSpec expects an escaped value. 
                    if (_updateToAbsolutePaths)
                    {
                        item.ItemSpec = EscapingUtilities.Escape(fullPath);
                    }

                    inPathList.Add(item);
                }
                else
                {
                    outOfPathList.Add(item);
                }
            }

            InPath = (ITaskItem[])inPathList.ToArray(typeof(ITaskItem));
            OutOfPath = (ITaskItem[])outOfPathList.ToArray(typeof(ITaskItem));
            return true;
        }
    }
}
