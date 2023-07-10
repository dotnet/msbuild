// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

#nullable disable

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
        public override readonly string ToString()
        {
            return assemblyName + ", " + reference;
        }

        /// <summary>
        /// Compare by assembly name.
        /// </summary>
        public readonly int CompareTo(AssemblyNameReference other)
        {
            return assemblyName.CompareTo(other.assemblyName);
        }

        /// <summary>
        /// Construct a new AssemblyNameReference.
        /// </summary>
        public static AssemblyNameReference Create(AssemblyNameExtension assemblyName, Reference reference)
        {
            AssemblyNameReference result;
            result.assemblyName = assemblyName;
            result.reference = reference;
            return result;
        }
    }
}
