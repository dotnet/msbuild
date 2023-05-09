// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {AssemblyFolders}
    /// </summary>
    internal class AssemblyFoldersResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="searchPathElement">The corresponding element from the search path.</param>
        /// <param name="getAssemblyName">Delegate that gets the assembly name.</param>
        /// <param name="fileExists">Delegate that returns if the file exists.</param>
        /// <param name="getRuntimeVersion">Delegate that returns the clr runtime version for the file.</param>
        /// <param name="targetedRuntimeVesion">The targeted runtime version.</param>
        public AssemblyFoldersResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, System.Reflection.ProcessorArchitecture.None, false)
        {
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

#if FEATURE_WIN32_REGISTRY
            if (assemblyName != null)
            {
                // {AssemblyFolders} was passed in.
                foreach (string assemblyFolder in AssemblyFolder.GetAssemblyFolders(assemblyFolderKey))
                {
                    string resolvedPath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, assemblyFolder, assembliesConsideredAndRejected);
                    if (resolvedPath != null)
                    {
                        foundPath = resolvedPath;
                        return true;
                    }
                }
            }
#endif
            return false;
        }
    }
}
