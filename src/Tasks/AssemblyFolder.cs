// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if FEATURE_WIN32_REGISTRY

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Win32;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Contains utility functions for dealing with assembly folders found in the registry.
    /// </summary>
    internal static class AssemblyFolder
    {
        /// <summary>
        /// Key -- Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project
        /// Value -- Directory
        /// </summary>
        private static Dictionary<string, string> s_assemblyFolders;

        /// <summary>
        /// Synchronize the creation of assemblyFolders
        /// </summary>
        private static readonly Object s_syncLock = new Object();

        /// <summary>
        /// Given a registry key, find all of the registered assembly folders and add them to the list.
        /// </summary>
        /// <param name="hive">Like 'hklm' or 'hkcu'</param>
        /// <param name="key">The registry key to examine</param>
        /// <param name="directories">The object to populate</param>
        private static void AddFoldersFromRegistryKey
        (
            RegistryKey hive,
            string key,
            Dictionary<string, string> directories
        )
        {
            using (RegistryKey baseKey = hive.OpenSubKey(key))
            {
                string aliasKey = String.Empty;

                if (hive == Registry.CurrentUser)
                {
                    aliasKey = "hkcu";
                }
                else if (hive == Registry.LocalMachine)
                {
                    aliasKey = "hklm";
                }
                else
                {
                    ErrorUtilities.VerifyThrow(false, "AssemblyFolder.AddFoldersFromRegistryKey expected a known hive.");
                }

                if (baseKey != null)
                {
                    foreach (string productName in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey product = baseKey.OpenSubKey(productName))
                        {
                            if (product.ValueCount > 0)
                            {
                                string folder = (string)product.GetValue("");
                                if (Directory.Exists(folder))
                                {
                                    string regkeyAlias = aliasKey + "\\" + productName;
                                    directories[regkeyAlias] = folder;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For the given key name, look for registered assembly folders in HKCU then HKLM.
        /// </summary>
        private static void AddFoldersFromRegistryKey(
            string key,
            Dictionary<string, string> directories
        )
        {
            // First add the current user.
            AddFoldersFromRegistryKey
            (
                Registry.CurrentUser,
                key,
                directories
            );

            // Then add the local machine.            
            AddFoldersFromRegistryKey
            (
                Registry.LocalMachine,
                key,
                directories
            );
        }

        /// <summary>
        /// Populates the internal tables.
        /// </summary>
        private static void CreateAssemblyFolders()
        {
            s_assemblyFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (NativeMethodsShared.IsWindows)
            {
                // Populate the table of assembly folders.
                AddFoldersFromRegistryKey
                (
                    @"SOFTWARE\Microsoft\.NETFramework\AssemblyFolders",
                    s_assemblyFolders
                );

                AddFoldersFromRegistryKey
                (
                    @"SOFTWARE\Microsoft\VisualStudio\8.0\AssemblyFolders",
                    s_assemblyFolders
                );
            }
        }

        /// <summary>
        /// Returns the list of assembly folders that we're interested in.
        /// </summary>
        /// <param name="regKeyAlias">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        internal static IEnumerable<string> GetAssemblyFolders(string regKeyAlias)
        {
            lock (s_syncLock)
            {
                if (s_assemblyFolders == null)
                {
                    CreateAssemblyFolders();
                }
            }

            // If no specific alias was requested then return the complete list.
            if (string.IsNullOrEmpty(regKeyAlias))
            {
                foreach (string folder in s_assemblyFolders.Values)
                {
                    yield return folder;
                }
            }

            // If a specific alias was requested then return only that alias.
            if (s_assemblyFolders.TryGetValue(regKeyAlias, out string directory))
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    yield return directory;
                }
            }
        }
    }
}
#endif
