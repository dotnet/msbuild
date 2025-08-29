﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

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

        /// <inheritdoc/>
        public override bool Resolve(
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool isImmutableFrameworkReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            List<ResolutionSearchLocation> assembliesConsideredAndRejected,
            out string foundPath,
            out bool userRequestedSpecificFile)
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

            if (assemblyNameToUse.Version == null && installedAssemblies != null)
            {
                // If there are multiple entries in the redist list for this assembly, let's
                // pick the one with the highest version and resolve it.
                foreach (AssemblyEntry a in installedAssemblies.FindAssemblyNameFromSimpleName(assemblyName.Name))
                {
                    var current = new AssemblyNameExtension(a.FullName);

                    // If the current version is higher than the previously looked at.
                    if (current.Version?.CompareTo(assemblyNameToUse.Version) > 0)
                    {
                        // Only compare the Culture and the public key token, the simple names will ALWAYS be the same and the version we do not care about.
                        if (assemblyName.PartialNameCompare(current, PartialComparisonFlags.Culture | PartialComparisonFlags.PublicKeyToken))
                        {
                            assemblyNameToUse = current;
                        }
                    }
                }
            }

            return assemblyNameToUse;
        }
    }
}
