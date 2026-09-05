// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;


namespace Microsoft.Build.Shared.AssemblyFoldersFromConfig
{
    internal class AssemblyFoldersFromConfig<TInfo> : IEnumerable<TInfo>
    {
        /// <summary>
        /// The list of directory names found from the config file.
        /// </summary>
        private readonly List<TInfo> _directoryNames = new List<TInfo>();

        private readonly Func<string, Version, TInfo> _createInfo;

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="configFile">The path to the config file.</param>
        /// <param name="targetRuntimeVersion">The runtime version property from the project file.</param>
        /// <param name="targetArchitecture">The <see cref="ProcessorArchitecture"/> to target.</param>
        /// <param name="createInfo">Creates the assembly-folder information exposed by the consuming assembly.</param>
        internal AssemblyFoldersFromConfig(
            string configFile,
            string targetRuntimeVersion,
            ProcessorArchitecture targetArchitecture,
            Func<string, Version, TInfo> createInfo)
        {
            ArgumentNullException.ThrowIfNull(configFile);
            ArgumentNullException.ThrowIfNull(targetRuntimeVersion);
            ArgumentNullException.ThrowIfNull(createInfo);

            _createInfo = createInfo;

            var collection = AssemblyFolderCollection.Load(configFile);
            var assemblyTargets = GatherVersionStrings(targetRuntimeVersion, collection);

            bool targeting64Bit = targetArchitecture == ProcessorArchitecture.Amd64 ||
                                  targetArchitecture == ProcessorArchitecture.IA64;

            // Platform-agnostic folders first.
            FindDirectories(assemblyTargets, target => string.IsNullOrEmpty(target.Platform));

            if (Environment.Is64BitOperatingSystem)
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
                .Select(target => _createInfo(target.Path, GetFrameworkVersion(target.FrameworkVersion)));

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
        IEnumerator<TInfo> IEnumerable<TInfo>.GetEnumerator()
        {
            return _directoryNames.GetEnumerator();
        }

        /// <summary>
        /// Get enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TInfo>)this).GetEnumerator();
        }
    }
}
