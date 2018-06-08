// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Utility methods that encapsulate well-known assemblies.
    /// </summary>
    internal class InstalledAssemblies
    {
        private readonly RedistList _redistList;

        /// <summary>
        /// Construct.
        /// </summary>
        internal InstalledAssemblies(RedistList redistList)
        {
            _redistList = redistList;
        }

        /// <summary>
        /// Unify an assembly name according to the fx retarget rules.
        /// </summary>
        /// <param name="assemblyName">The unify-from assembly name.</param>
        /// <param name="unifiedVersion">The new version number.</param>
        /// <param name="isPrerequisite">Whether this assembly will be available on target machines.</param>
        /// <param name="isRedistRoot">May be true, false or null. Null means there was no IsRedistRoot in the redist list.</param>
        /// <param name="redistName">Name of the corresponding Resist specified in the redist list.</param>
        internal void GetInfo
        (
            AssemblyNameExtension assemblyName,
            out Version unifiedVersion,
            out bool isPrerequisite,
            out bool? isRedistRoot,
            out string redistName
        )
        {
            unifiedVersion = assemblyName.Version;
            isPrerequisite = false;
            isRedistRoot = null;
            redistName = null;

            // Short-circuit in cases where there is no redist list.
            if (_redistList == null)
            {
                return;
            }

            // If there's no version, for example in a simple name, then no remapping is possible,
            // and this is not a prerequisite.
            if (assemblyName.Version == null)
            {
                return;
            }

            AssemblyEntry highestVersionFromRedistList = FindHighestVersionInRedistList(assemblyName);

            // Could not find the assembly in the redist list. Return as there has been no redist list unification
            if (highestVersionFromRedistList == null)
            {
                return;
            }

            // Dont allow downgrading of reference version due to redist unification because this is automatic rather than something like an appconfig which 
            // has to be manually set. However if the major version is 255 then we do want to unify down the version number.
            if (assemblyName.Version <= highestVersionFromRedistList.AssemblyNameExtension.Version || assemblyName.Version.Major == 255)
            {
                unifiedVersion = highestVersionFromRedistList.AssemblyNameExtension.Version;
                isPrerequisite = _redistList.IsPrerequisiteAssembly(highestVersionFromRedistList.FullName);
                isRedistRoot = _redistList.IsRedistRoot(highestVersionFromRedistList.FullName);
                redistName = _redistList.RedistName(highestVersionFromRedistList.FullName);
            }
        }

        /// <summary>
        /// We need to check to see if an assembly name is in our remapping list, if it is we should return a new assemblyNameExtension which has been remapped.
        /// Remapping is usually used for portable libraries where we need to turn one assemblyName that is retargetable to another assemblyname.
        /// </summary>
        internal AssemblyNameExtension RemapAssemblyExtension(AssemblyNameExtension assemblyName)
        {
            // Short-circuit in cases where there is no redist list
            return _redistList?.RemapAssembly(assemblyName);
        }

        /// <summary>
        /// Find the highest version of the assemblyName in the redist list for framework assemblies taking into account the simplename, culture and public key.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly we would like to find the highest version for</param>
        /// <returns>Key value pair, K: Assembly entry of highest value in the redist list. V: AssemblyNameExtension with the version information or null if the name could not be found</returns>
        internal AssemblyEntry FindHighestVersionInRedistList(AssemblyNameExtension assemblyName)
        {
            // The assembly we are looking for is not listed in a redist list which contains framework assemblies. We do not want to find 
            // find non framework assembly entries.
            if (!FrameworkAssemblyEntryInRedist(assemblyName))
            {
                return null;
            }

            // Look up an assembly with the same base name in the installedAssemblyTables.
            // This list should be sorted alphabetically by simple name and then greatest verion
            AssemblyEntry[] tableCandidates = _redistList.FindAssemblyNameFromSimpleName(assemblyName.Name);

            foreach (AssemblyEntry tableCandidate in tableCandidates)
            {
                // Make an AssemblyNameExtension for comparing.
                AssemblyNameExtension mostRecentAssemblyNameCandidate = tableCandidate.AssemblyNameExtension;

                // Optimize performance for the whidbey case by doing an exact comparison first.
                if (mostRecentAssemblyNameCandidate.EqualsIgnoreVersion(assemblyName))
                {
                    return tableCandidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Given an assemblyNameExtension, is that assembly name in the redist list and does that redist name start with Microsoft-Windows-CLRCoreComp which indicates
        /// the redist entry is a framework redist list rather than a 3rd part redist list.
        /// </summary>
        internal bool FrameworkAssemblyEntryInRedist(AssemblyNameExtension assemblyName)
        {
            if (_redistList == null)
            {
                return false;
            }

            return _redistList.FrameworkAssemblyEntryInRedist(assemblyName);
        }

        /// <summary>
        /// Find every assembly full name in the redist list that matches the given simple name.
        /// </summary>
        /// <param name="simpleName"></param>
        /// <returns>The array of assembly names.</returns>
        internal AssemblyEntry[] FindAssemblyNameFromSimpleName(string simpleName)
        {
            if (_redistList == null)
            {
                return Array.Empty<AssemblyEntry>();
            }

            return _redistList.FindAssemblyNameFromSimpleName(simpleName);
        }
    }
}
