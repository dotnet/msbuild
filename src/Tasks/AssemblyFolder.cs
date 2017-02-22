// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if FEATURE_WIN32_REGISTRY

using System;
using System.IO;
using System.Collections;

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
        private static Hashtable s_assemblyFolders;

        /// <summary>
        /// Synchronize the creation of assemblyFolders
        /// </summary>
        private static Object s_syncLock = new Object();

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
            Hashtable directories
        )
        {
            if (!NativeMethodsShared.IsWindows && hive == Registry.LocalMachine)
            {
                string path = NativeMethodsShared.FrameworkBasePath;
                if (Directory.Exists(path))
                {
                    foreach (var p in Directory.EnumerateDirectories(path))
                    {
                        directories[
                            "hklm" + "\\" + p.Substring(p.LastIndexOf(Path.DirectorySeparatorChar) + 1)] = p;
                    }
                }

                return;
            }

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
        /// <param name="key"></param>
        /// <param name="directories"></param>
        private static void AddFoldersFromRegistryKey
        (
            string key,
            Hashtable directories
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
            s_assemblyFolders = new Hashtable(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>
        /// Returns the list of assembly folders that we're interested in.
        /// </summary>
        /// <param name="regKeyAlias">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <returns>Collection of assembly folders.</returns>
        static internal ICollection GetAssemblyFolders(string regKeyAlias)
        {
            lock (s_syncLock)
            {
                if (s_assemblyFolders == null)
                {
                    CreateAssemblyFolders();
                }
            }

            // If no specific alias was requested then return the complete list.
            if (regKeyAlias == null || regKeyAlias.Length == 0)
            {
                return s_assemblyFolders.Values;
            }

            // If a specific alias was requested then return only that alias.
            ArrayList specificKey = new ArrayList();
            string directory = (string)s_assemblyFolders[regKeyAlias];
            if (directory != null && directory.Length > 0)
            {
                specificKey.Add(directory);
            }
            return specificKey;
        }
    }
}
#endif
