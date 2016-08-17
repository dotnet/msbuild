// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// Contains information about entries in the AssemblyFoldersEx registry keys.
//-----------------------------------------------------------------------
#if FEATURE_WIN32_REGISTRY

using System;
using Microsoft.Build.Shared;
using Microsoft.Win32;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Contains information about entries in the AssemblyFoldersEx registry keys.
    /// </summary>
    public class AssemblyFoldersExInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AssemblyFoldersExInfo(RegistryHive hive, RegistryView view, string registryKey, string directoryPath, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKey, "registryKey");
            ErrorUtilities.VerifyThrowArgumentNull(directoryPath, "directoryPath");
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, "targetFrameworkVersion");

            Hive = hive;
            View = view;
            Key = registryKey;
            DirectoryPath = directoryPath;
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// Registry hive used
        /// </summary>
        public RegistryHive Hive
        {
            get;
            private set;
        }

        /// <summary>
        /// Registry view used
        /// </summary>
        public RegistryView View
        {
            get;
            private set;
        }

        /// <summary>
        /// The registry key to the component
        /// </summary>
        public string Key
        {
            get;
            private set;
        }

        /// <summary>
        /// Folder found at the registry keys default value
        /// </summary>
        public string DirectoryPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Target framework version for the registry key
        /// </summary>
        public Version TargetFrameworkVersion
        {
            get;
            private set;
        }
    }
}
#endif