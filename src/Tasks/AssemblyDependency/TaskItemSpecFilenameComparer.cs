// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Compare two ITaskItems by the file name in their ItemSpec.
    /// </summary>
    internal sealed class TaskItemSpecFilenameComparer : IComparer<ITaskItem>
    {
        internal static readonly IComparer<ITaskItem> GenericComparer = new TaskItemSpecFilenameComparer();

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
            if (ReferenceEquals(o1, o2))
            {
                return 0;
            }

            ITaskItem i1 = (ITaskItem)o1;
            ITaskItem i2 = (ITaskItem)o2;

            return Compare(i1, i2);
        }

        public int Compare(ITaskItem x, ITaskItem y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            string xItemSpec = x.ItemSpec;
            string yItemSpec = y.ItemSpec;

            int xFilenameStart = xItemSpec.LastIndexOfAny(FileMatcher.directorySeparatorCharacters);
            if (xFilenameStart == -1)
            {
                xFilenameStart = 0;
            }

            int yFilenameStart = yItemSpec.LastIndexOfAny(FileMatcher.directorySeparatorCharacters);
            if (yFilenameStart == -1)
            {
                yFilenameStart = 0;
            }

            int fileComparison = string.Compare(xItemSpec,
                xFilenameStart,
                yItemSpec,
                yFilenameStart,
                int.MaxValue, // all characters after the start index
                StringComparison.OrdinalIgnoreCase);
            if (fileComparison != 0)
            {
                return fileComparison;
            }

            return string.Compare(xItemSpec, yItemSpec, StringComparison.OrdinalIgnoreCase);
        }
    }
}
