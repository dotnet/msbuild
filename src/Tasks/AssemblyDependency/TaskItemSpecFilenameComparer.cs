// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Compare two ITaskItems by the file name in their ItemSpec.
    /// </summary>
    sealed internal class TaskItemSpecFilenameComparer : IComparer, IComparer<ITaskItem>
    {
        internal readonly static IComparer comparer = new TaskItemSpecFilenameComparer();
        internal readonly static IComparer<ITaskItem> genericComparer = new TaskItemSpecFilenameComparer();

        /// <summary>
        /// Private construct so there's only one instance.
        /// </summary>
        private TaskItemSpecFilenameComparer()
        {
        }

        /// <summary>
        /// Compare the two AssemblyNameReferences by file name, and if that is equal, by item spec.
        /// </summary>
        /// <remarks>
        /// Sorting by item spec allows these to be ordered consistently:
        /// c:\Regress315619\A\MyAssembly.dll
        /// c:\Regress315619\B\MyAssembly.dll
        /// </remarks>
        public int Compare(object o1, object o2)
        {
            if (Object.ReferenceEquals(o1, o2))
            {
                return 0;
            }

            ITaskItem i1 = (ITaskItem)o1;
            ITaskItem i2 = (ITaskItem)o2;

            return Compare(i1, i2);
        }

        public int Compare(ITaskItem x, ITaskItem y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return 0;
            }

            string f1 = Path.GetFileName(x.ItemSpec);
            string f2 = Path.GetFileName(y.ItemSpec);

            int fileComparison = String.Compare(f1, f2, StringComparison.OrdinalIgnoreCase);
            if (fileComparison != 0)
            {
                return fileComparison;
            }

            return String.Compare(x.ItemSpec, y.ItemSpec, StringComparison.OrdinalIgnoreCase);
        }
    }
}
