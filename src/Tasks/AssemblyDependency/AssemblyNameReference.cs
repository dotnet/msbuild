// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

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
