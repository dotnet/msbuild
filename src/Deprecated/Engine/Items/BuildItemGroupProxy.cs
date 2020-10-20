// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// An enumerable wrapper for a hashtable-by-name of BuildItemGroups that allows read-only 
    /// access to the items.
    /// </summary>
    /// <remarks>
    /// This class is designed to be passed to loggers.
    /// The expense of copying items is only incurred if and when 
    /// a logger chooses to enumerate over it.
    /// </remarks>
    /// <owner>danmose</owner>
    internal class BuildItemGroupProxy : IEnumerable
    {
        // Item group that this proxies
        private BuildItemGroup backingItemGroup;

        private BuildItemGroupProxy()
        { 
            // Do nothing
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="itemGroup">Item group this class should proxy</param>
        public BuildItemGroupProxy(BuildItemGroup itemGroup)
        {
            this.backingItemGroup = itemGroup;
        }

        /// <summary>
        /// Returns an enumerator that provides copies of the items
        /// in the backing item group.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            foreach (BuildItem item in backingItemGroup)
            {
                yield return new DictionaryEntry(item.Name, new TaskItem(item));
            }
        }
    }
}
