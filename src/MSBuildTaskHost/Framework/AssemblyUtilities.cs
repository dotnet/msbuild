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
#if !CLR2COMPATIBILITY
        private static Lazy<Assembly> s_entryAssembly = new Lazy<Assembly>(() => GetEntryAssembly());
        public static Assembly EntryAssembly => s_entryAssembly.Value;
#else
        public static Assembly EntryAssembly = GetEntryAssembly();
#endif

#if CLR2COMPATIBILITY
        /// <summary>
        /// Shim for the lack of <see cref="System.Reflection.IntrospectionExtensions.GetTypeInfo"/> in .NET 3.5.
        /// </summary>
        public static Type GetTypeInfo(this Type t)
        {
            return t;
        }
#endif

        public static AssemblyName CloneIfPossible(this AssemblyName assemblyNameToClone)
        {
#if CLR2COMPATIBILITY
            return (AssemblyName)assemblyNameToClone.Clone();
#else

            // NOTE: In large projects, this is called a lot. Avoid calling AssemblyName.Clone
            // because it clones the Version property (which is immutable) and the PublicKey property
            // and the PublicKeyToken property.
            //
            // While the array themselves are mutable - throughout MSBuild they are only ever
            // read from.
            AssemblyName name = new AssemblyName();
            name.Name = assemblyNameToClone.Name;
            name.SetPublicKey(assemblyNameToClone.GetPublicKey());
            name.SetPublicKeyToken(assemblyNameToClone.GetPublicKeyToken());
            name.Version = assemblyNameToClone.Version;
            name.Flags = assemblyNameToClone.Flags;
            name.ProcessorArchitecture = assemblyNameToClone.ProcessorArchitecture;

            name.CultureInfo = assemblyNameToClone.CultureInfo;
            name.HashAlgorithm = assemblyNameToClone.HashAlgorithm;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;
            name.CodeBase = assemblyNameToClone.CodeBase;
            name.KeyPair = assemblyNameToClone.KeyPair;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;

            return name;
#endif
        }

        private static Assembly GetEntryAssembly()
        {
            return System.Reflection.Assembly.GetEntryAssembly();
        }
    }
}
