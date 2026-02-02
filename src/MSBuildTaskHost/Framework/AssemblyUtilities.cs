// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Reflection;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains common reflection tasks
    /// </summary>
    internal static class AssemblyUtilities
    {
        public static Assembly EntryAssembly = GetEntryAssembly();

        /// <summary>
        /// Shim for the lack of <see cref="System.Reflection.IntrospectionExtensions.GetTypeInfo"/> in .NET 3.5.
        /// </summary>
        public static Type GetTypeInfo(this Type t)
        {
            return t;
        }

        public static AssemblyName CloneIfPossible(this AssemblyName assemblyNameToClone)
        {
            return (AssemblyName)assemblyNameToClone.Clone();
        }

        private static Assembly GetEntryAssembly()
        {
            return System.Reflection.Assembly.GetEntryAssembly();
        }
    }
}
