// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Contains a registry key string and some version information associated with it</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Shared;
using Microsoft.Win32;

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
            ErrorUtilities.VerifyThrowArgumentNull(registryKey, "registryKey");
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, "targetFrameworkVersion");

            RegistryKey = registryKey;
            ComponentVersion = null;
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal ExtensionFoldersRegistryKey(string registryKey, Version componentVersion, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKey, "registryKey");
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, "targetFrameworkVersion");

            RegistryKey = registryKey;
            ComponentVersion = componentVersion;
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        /// <summary>
        /// The registry key to the component
        /// </summary>
        internal string RegistryKey
        {
            get;
            private set;
        }

        /// <summary>
        /// Target framework version for the registry key
        /// </summary>
        internal Version ComponentVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Target framework version for the registry key
        /// </summary>
        internal Version TargetFrameworkVersion
        {
            get;
            private set;
        }
    }
}