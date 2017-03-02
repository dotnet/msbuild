// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Compare the two AssemblyNameReferences by version number.
    /// </summary>
    sealed internal class AssemblyNameReferenceAscendingVersionComparer : IComparer
    {
        internal readonly static IComparer comparer = new AssemblyNameReferenceAscendingVersionComparer();

        /// <summary>
        /// Private construct so there's only one instance.
        /// </summary>
        private AssemblyNameReferenceAscendingVersionComparer()
        {
        }

        /// <summary>
        /// Compare the two AssemblyNameReferences by version number.
        /// </summary>
        /// <param name="o1"></param>
        /// <param name="o2"></param>
        /// <returns></returns>
        public int Compare(object o1, object o2)
        {
            AssemblyNameReference i1 = (AssemblyNameReference)o1;
            AssemblyNameReference i2 = (AssemblyNameReference)o2;

            Version v1 = i1.assemblyName.Version;
            Version v2 = i2.assemblyName.Version;

            if (v1 == null)
            {
                v1 = new Version(0, 0);
            }

            if (v2 == null)
            {
                v2 = new Version(0, 0);
            }

            return v1.CompareTo(v2);
        }
    }
}
