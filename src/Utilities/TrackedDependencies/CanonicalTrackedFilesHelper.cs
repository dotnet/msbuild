// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.Utilities
{
    internal static class CanonicalTrackedFilesHelper
    {
        internal const int MaxLogCount = 100;

        /// <summary>
        /// Check that the given composite root contains all entries in the composite sub root
        /// </summary>
        /// <param name="compositeRoot">The root to look for all sub roots in</param>
        /// <param name="compositeSubRoot">The root that is comprised of subroots to look for</param>
        /// <returns></returns>
        internal static bool RootContainsAllSubRootComponents(string compositeRoot, string compositeSubRoot)
        {
            // If the two are identical, then clearly all keys are present
            if (string.Compare(compositeRoot, compositeSubRoot, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            // look for each sub key in the main composite key
            string[] rootComponents = compositeSubRoot.Split(MSBuildConstants.PipeChar);
            foreach (string subRoot in rootComponents)
            {
                // we didn't find this subkey, so bail out
                if (!compositeRoot.Contains(subRoot))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This method checks that the specified files exist. During the scan the
        /// most recent file write time of all the outputs is remembered. It will be
        /// the basis for up to date comparisons.
        /// </summary>
        /// <param name="files">The files being checked for existence.</param>
        /// <param name="log">The TaskLoggingHelper used to log the nonexistent files.</param>
        /// <param name="outputNewestFilename">Name of the most recently modified file.</param>
        /// <param name="outputNewestTime">Timestamp of the most recently modified file.</param>
        /// <returns>True if all members of 'files' exist, false otherwise</returns>
        internal static bool FilesExistAndRecordNewestWriteTime(ICollection<ITaskItem> files, TaskLoggingHelper log, out DateTime outputNewestTime, out string outputNewestFilename)
            => FilesExistAndRecordRequestedWriteTime(files, log, true /* return information about the newest file */, out outputNewestTime, out outputNewestFilename);

        /// <summary>
        /// This method checks that the specified files exist. During the scan the
        /// least recent file write time of all the outputs is remembered. It will be
        /// the basis for up to date comparisons.
        /// </summary>
        /// <param name="files">The files being checked for existence.</param>
        /// <param name="log">The TaskLoggingHelper used to log the nonexistent files.</param>
        /// <param name="outputOldestFilename">Name of the least recently modified file.</param>
        /// <param name="outputOldestTime">Timestamp of the least recently modified file.</param>
        /// <returns>True if all members of 'files' exist, false otherwise</returns>
        internal static bool FilesExistAndRecordOldestWriteTime(ICollection<ITaskItem> files, TaskLoggingHelper log, out DateTime outputOldestTime, out string outputOldestFilename)
            => FilesExistAndRecordRequestedWriteTime(files, log, false /* return information about the oldest file */, out outputOldestTime, out outputOldestFilename);

        private static bool FilesExistAndRecordRequestedWriteTime(ICollection<ITaskItem> files, TaskLoggingHelper log, bool getNewest, out DateTime requestedTime, out string requestedFilename)
        {
            bool allExist = true;
            requestedTime = getNewest ? DateTime.MinValue : DateTime.MaxValue;
            requestedFilename = string.Empty;

            // No output files for the source were tracked
            // safely assume that this is because we didn't track them because they weren't compiled
            if (files == null || files.Count == 0)
            {
                allExist = false;
            }
            else
            {
                foreach (ITaskItem item in files)
                {
                    DateTime lastWriteTime = NativeMethodsShared.GetLastWriteFileUtcTime(item.ItemSpec);
                    // If the file does not exist
                    if (lastWriteTime == DateTime.MinValue)
                    {
                        FileTracker.LogMessageFromResources(log, MessageImportance.Low, "Tracking_OutputDoesNotExist", item.ItemSpec);
                        allExist = false;
                        break;
                    }

                    if (getNewest && lastWriteTime > requestedTime || !getNewest && lastWriteTime < requestedTime)
                    {
                        requestedTime = lastWriteTime;
                        requestedFilename = item.ItemSpec;
                    }
                }
            }
            return allExist;
        }
    }
}

#endif
