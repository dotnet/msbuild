// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.AssemblyFoldersFromConfig;
using Microsoft.Build.Utilities;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.Tasks.AssemblyFoldersFromConfig
{
    internal class AssemblyFoldersFromConfig : IEnumerable<AssemblyFoldersFromConfigInfo>
    {
        /// <summary>
        /// The list of directory names found from the config file.
        /// </summary>
        private readonly List<AssemblyFoldersFromConfigInfo> _directoryNames = new List<AssemblyFoldersFromConfigInfo>();

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="configFile">The path to the config file.</param>
        /// <param name="targetRuntimeVersion">The runtime version property from the project file.</param>
        /// <param name="targetArchitecture">The <see cref="ProcessorArchitecture"/> to target.</param>
        internal AssemblyFoldersFromConfig(string configFile, string targetRuntimeVersion, ProcessorArchitecture targetArchitecture)
        {
            ErrorUtilities.VerifyThrowArgumentNull(configFile, nameof(configFile));
            ErrorUtilities.VerifyThrowArgumentNull(targetRuntimeVersion, nameof(targetRuntimeVersion));
            
            var collection = AssemblyFolderCollection.Load(configFile);
            var assemblyTargets = GatherVersionStrings(targetRuntimeVersion, collection);

            bool targeting64Bit = targetArchitecture == ProcessorArchitecture.Amd64 ||
                                  targetArchitecture == ProcessorArchitecture.IA64;

            // Platform-agnostic folders first.
            FindDirectories(assemblyTargets, target => string.IsNullOrEmpty(target.Platform));

            if (EnvironmentUtilities.Is64BitOperatingSystem)
            {
                if (targeting64Bit)
                {
                    FindDirectories(assemblyTargets,
                        target => !string.IsNullOrEmpty(target.Platform) && target.Platform.Equals("x64", StringComparison.OrdinalIgnoreCase));
                    FindDirectories(assemblyTargets,
                        target => !string.IsNullOrEmpty(target.Platform) && target.Platform.Equals("x86", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    FindDirectories(assemblyTargets,
                        target => !string.IsNullOrEmpty(target.Platform) && target.Platform.Equals("x86", StringComparison.OrdinalIgnoreCase));
                    FindDirectories(assemblyTargets,
                        target => !string.IsNullOrEmpty(target.Platform) && target.Platform.Equals("x64", StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                FindDirectories(assemblyTargets,
                    target => !string.IsNullOrEmpty(target.Platform) && target.Platform.Equals("x86", StringComparison.OrdinalIgnoreCase));
            }
        }

        private void FindDirectories(List<AssemblyFolderItem> assemblyTargets, Func<AssemblyFolderItem, bool> platformFilter)
        {
            var targets = assemblyTargets
                .Where(platformFilter)
                .Select(target => new AssemblyFoldersFromConfigInfo(target.Path, GetFrameworkVersion(target.FrameworkVersion)));

            _directoryNames.AddRange(targets);
        }

        private static List<AssemblyFolderItem> GatherVersionStrings(string targetRuntimeVersion, AssemblyFolderCollection collection)
        {
            return 
                (from folder in collection.AssemblyFolders
                 let targetVersion = VersionUtilities.ConvertToVersion(targetRuntimeVersion)
                 let replacementVersion = GetFrameworkVersion(folder.FrameworkVersion)
                 
                 where targetVersion != null && targetVersion >= replacementVersion
                 orderby folder.FrameworkVersion descending 
                 select folder).ToList();
        }

        private static Version GetFrameworkVersion(string version)
        {
            var candidateVersion = VersionUtilities.ConvertToVersion(version);
            return new Version(candidateVersion.Major, candidateVersion.Minor);
        }

        /// <summary>
        /// Get Enumerator
        /// </summary>
        IEnumerator<AssemblyFoldersFromConfigInfo> IEnumerable<AssemblyFoldersFromConfigInfo>.GetEnumerator()
        {
            return _directoryNames.GetEnumerator();
        }

        /// <summary>
        /// Get enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<AssemblyFoldersFromConfigInfo>)this).GetEnumerator();
        }
    }
}
