// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Compare the two AssemblyNameReferences by version number.
    /// </summary>
    internal sealed class AssemblyNameReferenceAscendingVersionComparer : IComparer<AssemblyNameReference>
    {
        internal static readonly IComparer<AssemblyNameReference> comparer = new AssemblyNameReferenceAscendingVersionComparer();

        private static Version DummyVersion { get; } = new Version(0, 0);

        /// <summary>
        /// Private construct so there's only one instance.
        /// </summary>
        private AssemblyNameReferenceAscendingVersionComparer()
        {
        }

        /// <summary>
        /// Compare the two AssemblyNameReferences by version number.
        /// </summary>
        public int Compare(AssemblyNameReference i1, AssemblyNameReference i2)
        {
            Version v1 = i1.assemblyName.Version;
            Version v2 = i2.assemblyName.Version;

            if (v1 == null)
            {
                v1 = DummyVersion;
            }

            if (v2 == null)
            {
                v2 = DummyVersion;
            }

            return v1.CompareTo(v2);
        }
    }
}
