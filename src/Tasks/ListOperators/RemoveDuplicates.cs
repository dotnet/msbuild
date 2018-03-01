// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Given a list of items, remove duplicate items. Attributes are not considered. Case insensitive.
    /// </summary>
    public class RemoveDuplicates : TaskExtension
    {
        /// <summary>
        /// The left-hand set of items to be RemoveDuplicatesed from.
        /// </summary>
        public ITaskItem[] Inputs { get; set; } = Array.Empty<TaskItem>();

        /// <summary>
        /// List of unique items.
        /// </summary>
        [Output]
        public ITaskItem[] Filtered { get; set; } = null;

        /// <summary>
        /// True if any duplicate items were found. False otherwise.
        /// </summary>
        [Output]
        public bool HadAnyDuplicates { get; set; } = false;

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            var alreadySeen = new Hashtable(Inputs.Length, StringComparer.OrdinalIgnoreCase);
            var filteredList = new ArrayList();
            foreach (ITaskItem item in Inputs)
            {
                if (!alreadySeen.ContainsKey(item.ItemSpec))
                {
                    alreadySeen[item.ItemSpec] = String.Empty;
                    filteredList.Add(item);
                }
            }

            Filtered = (ITaskItem[])filteredList.ToArray(typeof(ITaskItem));
            HadAnyDuplicates = Inputs.Length != Filtered.Length;

            return true;
        }
    }
}
