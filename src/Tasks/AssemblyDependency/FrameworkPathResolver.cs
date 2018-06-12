// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {TargetFrameworkDirectory}
    /// </summary>
    internal class FrameworkPathResolver : Resolver
    {
        // Paths to FX folders.
        private readonly string[] _frameworkPaths;

        // Table of information about framework assemblies.
        private readonly InstalledAssemblies _installedAssemblies;

        /// <summary>
        /// Construct.
        /// </summary>
        public FrameworkPathResolver(string[] frameworkPaths, InstalledAssemblies installedAssemblies, string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, System.Reflection.ProcessorArchitecture.None, false)
        {
            _frameworkPaths = frameworkPaths;
            _installedAssemblies = installedAssemblies;
        }

        /// <summary>
        /// Resolve a reference to a specific file name.
        /// </summary>
        /// <param name="assemblyName">The assemblyname of the reference.</param>
        /// <param name="sdkName"></param>
        /// <param name="rawFileNameCandidate">The reference's 'include' treated as a raw file name.</param>
        /// <param name="isPrimaryProjectReference">Whether or not this reference was directly from the project file (and therefore not a dependency)</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">Allowed executable extensions.</param>
        /// <param name="hintPath">The item's hintpath value.</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <param name="foundPath">The path where the file was found.</param>
        /// <param name="userRequestedSpecificFile">Whether or not the user wanted a specific file (for example, HintPath is a request for a specific file)</param>
        /// <returns>True if the file was resolved.</returns>
        public override bool Resolve
        (
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            ArrayList assembliesConsideredAndRejected,
            out string foundPath,
            out bool userRequestedSpecificFile
        )
        {
            foundPath = null;
            userRequestedSpecificFile = false;

            if (assemblyName != null)
            {
                AssemblyNameExtension assemblyNameToUse = GetHighestVersionInRedist(_installedAssemblies, assemblyName);

                foreach (string frameworkPath in _frameworkPaths)
                {
                    string resolvedPath = ResolveFromDirectory(assemblyNameToUse, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, frameworkPath, assembliesConsideredAndRejected);

                    if (resolvedPath != null)
                    {
                        foundPath = resolvedPath;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// If the version is not set for an assembly reference, go through the redist list and find the highest version for that assembly.
        /// Make sure when matching the assembly in the redist that we take into account the publicKeyToken and the Culture.
        /// </summary>
        internal static AssemblyNameExtension GetHighestVersionInRedist(InstalledAssemblies installedAssemblies, AssemblyNameExtension assemblyName)
        {
            AssemblyNameExtension assemblyNameToUse = assemblyName;

            if ((assemblyNameToUse.Version == null && installedAssemblies != null))
            {
                // If there are multiple entries in the redist list for this assembly, let's
                // pick the one with the highest version and resolve it.

                AssemblyEntry[] assemblyEntries = installedAssemblies.FindAssemblyNameFromSimpleName(assemblyName.Name);

                if (assemblyEntries.Length != 0)
                {
                    for (int i = 0; i < assemblyEntries.Length; ++i)
                    {
                        var current = new AssemblyNameExtension(assemblyEntries[i].FullName);

                        // If the current version is higher than the previously looked at.
                        if (current.Version != null && current.Version.CompareTo(assemblyNameToUse.Version) > 0)
                        {
                            // Only compare the Culture and the public key token, the simple names will ALWAYS be the same and the version we do not care about.
                            if (assemblyName.PartialNameCompare(current, PartialComparisonFlags.Culture | PartialComparisonFlags.PublicKeyToken))
                            {
                                assemblyNameToUse = current;
                            }
                        }
                    }
                }
            }

            return assemblyNameToUse;
        }
    }
}
