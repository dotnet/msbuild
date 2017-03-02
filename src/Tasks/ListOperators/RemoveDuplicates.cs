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
    /// Given a list of items, remove duplicate items. Attributes are not considered. Case insensitive.
    /// </summary>
    public class RemoveDuplicates : TaskExtension
    {
        private ITaskItem[] _inputs = new TaskItem[0];
        private ITaskItem[] _filtered = null;

        /// <summary>
        /// The left-hand set of items to be RemoveDuplicatesed from.
        /// </summary>
        public ITaskItem[] Inputs
        {
            get { return _inputs; }
            set { _inputs = value; }
        }

        /// <summary>
        /// List of unique items.
        /// </summary>
        [Output]
        public ITaskItem[] Filtered
        {
            get { return _filtered; }
            set { _filtered = value; }
        }

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Hashtable alreadySeen = new Hashtable(Inputs.Length, StringComparer.OrdinalIgnoreCase);
            ArrayList filteredList = new ArrayList();
            foreach (ITaskItem item in Inputs)
            {
                if (!alreadySeen.ContainsKey(item.ItemSpec))
                {
                    alreadySeen[item.ItemSpec] = String.Empty;
                    filteredList.Add(item);
                }
            }

            Filtered = (ITaskItem[])filteredList.ToArray(typeof(ITaskItem));

            return true;
        }
    }
}
