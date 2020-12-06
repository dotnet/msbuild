// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Implementation of IComparer on ITaskItems used for testing.
    /// </summary>
    public class TaskItemComparer : IComparer<ITaskItem>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        private TaskItemComparer()
        {
        }

        /// <summary>
        /// Retrieves a new instance of the class.
        /// </summary>
        public static IComparer<ITaskItem> Instance
        {
            get { return new TaskItemComparer(); }
        }

        #region IComparer<ITaskItem> Members

        /// <summary>
        /// Compares two task items.
        /// Built-in derivable metadata is ignored as it might not be copied.
        /// </summary>
        /// <returns>0 if they are equal, -1 otherwise.</returns>
        public int Compare(ITaskItem x, ITaskItem y)
        {
            if (x.ItemSpec != y.ItemSpec)
            {
                return -1;
            }

            if (x.CloneCustomMetadata().Count != y.CloneCustomMetadata().Count)
            {
                return -1;
            }

            foreach (string metadataName in x.MetadataNames)
            {
                if (!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName) ||
                    FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName))
                {
                    if (x.GetMetadata(metadataName) != y.GetMetadata(metadataName))
                    {
                        return -1;
                    }
                }
            }

            foreach (string metadataName in y.MetadataNames)
            {
                if (!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName) ||
                    FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName))
                {
                    if (x.GetMetadata(metadataName) != y.GetMetadata(metadataName))
                    {
                        return -1;
                    }
                }
            }

            return 0;
        }

        #endregion
    }
}
