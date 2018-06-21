// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// A static cache that will hold the dependency graph as built from tlog files.
    /// The cache is keyed on the root marker created from the full paths of the tlog files concerned.
    /// As an entry is added to the cache so is the datetime it was added.
    /// </summary>
    internal static class DependencyTableCache
    {
        private static readonly char[] s_numerals = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static readonly TaskItemItemSpecIgnoreCaseComparer s_taskItemComparer = new TaskItemItemSpecIgnoreCaseComparer();

        /// <summary>
        /// The dictionary that maps the root of the tlog filenames to the dependencytable built from their content
        /// </summary>
        internal static Dictionary<string, DependencyTableCacheEntry> DependencyTable { get; } = new Dictionary<string, DependencyTableCacheEntry>(StringComparer.OrdinalIgnoreCase);

        #region Methods
        /// <summary>
        /// Determine if a cache entry is up to date
        /// </summary>
        /// <param name="dependencyTable">The cache entry to check</param>
        /// <returns>true if up to date</returns>
        private static bool DependencyTableIsUpToDate(DependencyTableCacheEntry dependencyTable)
        {
            DateTime tableTime = dependencyTable.TableTime;

            foreach (ITaskItem tlogFile in dependencyTable.TlogFiles)
            {
                string tlogFilename = FileUtilities.NormalizePath(tlogFile.ItemSpec);

                DateTime lastWriteTime = NativeMethodsShared.GetLastWriteFileUtcTime(tlogFilename);
                if (lastWriteTime > tableTime)
                {
                    // one of the tlog files is newer than the table, so return false
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the cached entry for the given tlog set, if the table is out of date it is removed from the cache
        /// </summary>
        /// <param name="tLogRootingMarker">The rooting marker for the set of tlogs</param>
        /// <returns>The cached table entry</returns>
        internal static DependencyTableCacheEntry GetCachedEntry(string tLogRootingMarker)
        {
            if (DependencyTable.ContainsKey(tLogRootingMarker))
            {
                DependencyTableCacheEntry cacheEntry = DependencyTable[tLogRootingMarker];
                if (DependencyTableIsUpToDate(cacheEntry))
                {
                    return cacheEntry;
                }
                else
                {
                    // Remove the cached entry from memory
                    DependencyTable.Remove(tLogRootingMarker);
                }
            }
            // Either there was no cache entry, or it was out of date and was removed
            return null;
        }

        /// <summary>
        /// Given a set of TLog names, formats a rooting marker from them, that additionally replaces 
        /// all PIDs and TIDs with "[ID]" so the cache doesn't get overloaded with entries 
        /// that should be basically the same but have different PIDs or TIDs in the name. 
        /// </summary>
        /// <param name="tlogFiles">The set of tlogs to format</param>
        /// <returns>The normalized rooting marker based on that set of tlogs</returns>
        internal static string FormatNormalizedTlogRootingMarker(ITaskItem[] tlogFiles)
        {
            var normalizedFiles = new HashSet<ITaskItem>(s_taskItemComparer);

            for (int i = 0; i < tlogFiles.Length; i++)
            {
                ITaskItem normalizedFile = new TaskItem(tlogFiles[i]);
                normalizedFile.ItemSpec = NormalizeTlogPath(tlogFiles[i].ItemSpec);
                normalizedFiles.Add(normalizedFile);
            }

            string normalizedRootingMarker = FileTracker.FormatRootingMarker(normalizedFiles.ToArray());
            return normalizedRootingMarker;
        }

        /// <summary>
        /// Given a TLog path, replace all PIDs and TIDs with "[ID]" in the filename, where 
        /// the typical format of a filename is "tool[.PID][-tool].read/write/command/delete.TID.tlog"
        /// </summary>
        /// <comments>
        /// The algorithm used finds all instances of .\d+. and .\d+- in the filename and translates them
        /// to .[ID]. and .[ID]- respectively, where "filename" is defined as the part of the path following 
        /// the final '\' in the path.  
        /// 
        /// In the VS 2010 C++ project system, there are artificially constructed tlogs that instead follow the 
        /// pattern "ProjectName.read/write.1.tlog", which means that one result of this change is that such 
        /// tlogs, should the project name also contain this pattern (e.g. ClassLibrary.1.csproj), will also end up
        /// with [ID] being substituted for digits in the project name itself -- so the tlog name would end up being 
        /// ClassLibrary.[ID].read.[ID].tlog, rather than ClassLibrary.1.read.[ID].tlog.  This could potentially 
        /// cause issues if there are multiple projects differentiated only by the digits in their names; however 
        /// we believe this is not an interesting scenario to watch for and support, given that the resultant rooting 
        /// marker is constructed from full paths, so either: 
        /// - The project directories are also different, and are never substituted, leading to different full paths (e.g. 
        ///   C:\ClassLibrary.1\Debug\ClassLibrary.[ID].read.[ID].tlog and C:\ClassLibrary.2\Debug\ClassLibrary.[ID].read.[ID].tlog)
        /// - The project directories are the same, in which case there are two projects that share the same intermediate 
        ///   directory, which has a host of other problems and is explicitly NOT a supported scenario.  
        /// </comments>
        /// <param name="tlogPath">The tlog path to normalize</param>
        /// <returns>The normalized path</returns>
        private static string NormalizeTlogPath(string tlogPath)
        {
            if (tlogPath.IndexOfAny(s_numerals) == -1)
            {
                // no reason to make modifications if there aren't any numerical IDs in the 
                // log filename to begin with. 
                return tlogPath;
            }
            else
            {
                int i;
                StringBuilder normalizedTlogFilename = new StringBuilder();

                // We're walking the filename backwards since once we hit the final '\', we know we can stop parsing. 
                // So as to avoid allocating more memory and/or forcing StringBuilder to do more character copies 
                // than necessary, we append the reversed filename character by character to its own StringBuilder, 
                // and then reverse it again when constructing the final normalized path.  
                for (i = tlogPath.Length - 1; i >= 0 && tlogPath[i] != '\\'; i--)
                {
                    // final character in the pattern can be either '.' or '-'
                    if (tlogPath[i] == '.' || tlogPath[i] == '-')
                    {
                        normalizedTlogFilename.Append(tlogPath[i]);

                        int j = i - 1;
                        // to match the pattern, all preceding characters must be numeric
                        while (j >= 0 && tlogPath[j] != '\\' && tlogPath[j] >= '0' && tlogPath[j] <= '9')
                        {
                            j--;
                        }

                        // and the pattern must begin with '.'
                        if (j >= 0 && tlogPath[j] == '.')
                        {
                            // [ID] backwards. :)
                            normalizedTlogFilename.Append("]DI[");
                            normalizedTlogFilename.Append(tlogPath[j]);
                            i = j;
                        }
                    }
                    else
                    {
                        // append this character -- it's not interesting. 
                        normalizedTlogFilename.Append(tlogPath[i]);
                    }
                }

                StringBuilder normalizedTlogPath = new StringBuilder(i + normalizedTlogFilename.Length);

                if (i >= 0)
                {
                    // If we bailed out early, add everything else before reversing the filename itself
                    normalizedTlogPath.Append(tlogPath.Substring(0, i + 1));
                }

                // now add the reversed filename
                for (int k = normalizedTlogFilename.Length - 1; k >= 0; k--)
                {
                    normalizedTlogPath.Append(normalizedTlogFilename[k]);
                }

                return normalizedTlogPath.ToString();
            }
        }

        #endregion

        #region TaskItemItemSpecIgnoreCaseComparer

        /// <summary>
        /// EqualityComparer for ITaskItems that only looks at the itemspec
        /// </summary>
        private class TaskItemItemSpecIgnoreCaseComparer : IEqualityComparer<ITaskItem>
        {
            /// <summary>
            /// Returns whether the two ITaskItems are equal, where they are judged to be 
            /// equal as long as the itemspecs, compared case-insensitively, are equal. 
            /// </summary>
            public bool Equals(ITaskItem x, ITaskItem y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                {
                    return false;
                }

                return string.Equals(x.ItemSpec, y.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Returns the hashcode of this ITaskItem.  Given that equality is judged solely based
            /// on the itemspec, the hash code for this particular comparer also only uses the 
            /// itemspec to make its determination. 
            /// </summary>
            public int GetHashCode(ITaskItem obj) => obj == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ItemSpec);
        }

        #endregion
    }

    /// <summary>
    /// A cache entry
    /// </summary>
    internal class DependencyTableCacheEntry
    {
        // the set of tlog files used to build this cache entry
        public ITaskItem[] TlogFiles { get; }

        public DateTime TableTime { get; }

        public IDictionary DependencyTable { get; }

        /// <summary>
        /// Construct a new entry
        /// </summary>
        /// <param name="tlogFiles">The tlog files used to build this dependency table</param>
        /// <param name="dependencyTable">The dependency table to be cached</param>
        internal DependencyTableCacheEntry(ITaskItem[] tlogFiles, IDictionary dependencyTable)
        {
            TlogFiles = new ITaskItem[tlogFiles.Length];
            TableTime = DateTime.MinValue;

            // Our cache's knowledge of the tlog items needs their full path
            for (int tlogItemCount = 0; tlogItemCount < tlogFiles.Length; tlogItemCount++)
            {
                string tlogFilename = FileUtilities.NormalizePath(tlogFiles[tlogItemCount].ItemSpec);
                TlogFiles[tlogItemCount] = new TaskItem(tlogFilename);
                // Our cache entry needs to use the last modified time of the latest tlog
                // involved so that our cache can be invalidated if any tlog is updated
                DateTime modifiedTime = NativeMethodsShared.GetLastWriteFileUtcTime(tlogFilename);
                if (modifiedTime > TableTime)
                {
                    TableTime = modifiedTime;
                }
            }

            DependencyTable = dependencyTable;
        }
    }
}

#endif
