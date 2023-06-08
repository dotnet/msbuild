// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Win32;

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Contains information about entries in the AssemblyFoldersEx registry keys.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AssemblyFoldersExInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AssemblyFoldersExInfo(RegistryHive hive, RegistryView view, string registryKey, string directoryPath, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKey, nameof(registryKey));
            ErrorUtilities.VerifyThrowArgumentNull(directoryPath, nameof(directoryPath));
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, nameof(targetFrameworkVersion));

            Hive = hive;
            View = view;
            Key = registryKey;
            DirectoryPath = directoryPath;
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// Registry hive used
        /// </summary>
        public RegistryHive Hive { get; }

        /// <summary>
        /// Registry view used
        /// </summary>
        public RegistryView View { get; }

        /// <summary>
        /// The registry key to the component
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Folder found at the registry keys default value
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// Target framework version for the registry key
        /// </summary>
        public Version TargetFrameworkVersion { get; }
    }
}
