// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// An assembly name coupled with reference information.
    /// </summary>
    internal struct AssemblyNameReference : IComparable<AssemblyNameReference>
    {
        internal AssemblyNameExtension assemblyName;
        internal Reference reference;

        /// <summary>
        /// Display as string.
        /// </summary>
        public override string ToString()
        {
            return assemblyName + ", " + reference;
        }

        /// <summary>
        /// Compare by assembly name.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(AssemblyNameReference other)
        {
            return assemblyName.CompareTo(other.assemblyName);
        }

        /// <summary>
        /// Construct a new AssemblyNameReference.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static AssemblyNameReference Create(AssemblyNameExtension assemblyName, Reference reference)
        {
            AssemblyNameReference result;
            result.assemblyName = assemblyName;
            result.reference = reference;
            return result;
        }
    }
}
