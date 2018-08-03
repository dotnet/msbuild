// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

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
        public ITaskItem[] Inputs { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// List of unique items.
        /// </summary>
        [Output]
        public ITaskItem[] Filtered { get; set; }

        /// <summary>
        /// True if any duplicate items were found. False otherwise.
        /// </summary>
        [Output]
        public bool HadAnyDuplicates { get; set; }

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (Inputs == null || Inputs.Length == 0)
            {
                Filtered = Array.Empty<ITaskItem>();
                HadAnyDuplicates = false;
                return true;
            }

            var alreadySeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filteredList = new List<ITaskItem>(Inputs.Length);

            foreach (ITaskItem item in Inputs)
            {
                if (alreadySeen.Add(item.ItemSpec))
                {
                    filteredList.Add(item);
                }
            }

            Filtered = filteredList.ToArray();
            HadAnyDuplicates = Inputs.Length != Filtered.Length;
            return true;
        }
    }
}
