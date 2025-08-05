// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains common reflection tasks
    /// </summary>
    internal static class AssemblyUtilities
    {
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

#if !RUNTIME_TYPE_NETCORE
            name.CultureInfo = assemblyNameToClone.CultureInfo;
            name.HashAlgorithm = assemblyNameToClone.HashAlgorithm;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;
            name.CodeBase = assemblyNameToClone.CodeBase;
            name.KeyPair = assemblyNameToClone.KeyPair;
            name.VersionCompatibility = assemblyNameToClone.VersionCompatibility;
#else
            // Setting the culture name creates a new CultureInfo, leading to many allocations. Only set CultureName when the CultureInfo member is not available.
            name.CultureName = assemblyNameToClone.CultureName;
#endif

            return name;
#endif

        }
    }
}
