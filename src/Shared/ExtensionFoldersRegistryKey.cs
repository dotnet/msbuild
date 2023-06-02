// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Contains information about entries in the AssemblyFoldersEx registry keys.
    /// </summary>
    internal class ExtensionFoldersRegistryKey
    {
        /// <summary>
        /// Constructor
        /// </summary>
        internal ExtensionFoldersRegistryKey(string registryKey, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKey, nameof(registryKey));
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, nameof(targetFrameworkVersion));

            RegistryKey = registryKey;
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// The registry key to the component
        /// </summary>
        internal string RegistryKey { get; }

        /// <summary>
        /// Target framework version for the registry key
        /// </summary>
        internal Version TargetFrameworkVersion { get; }
    }
}
