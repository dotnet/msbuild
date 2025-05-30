// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve when the searchpath type is a simple directory name.
    /// </summary>
    internal class DirectoryResolver : Resolver
    {
        public readonly string parentAssembly;
        
        /// <summary>
        /// The parent assembly that was used for the SearchPath.
        /// </summary>
        public DirectoryResolver(
            string searchPathElement,
            GetAssemblyName getAssemblyName,
            FileExists fileExists,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Version targetedRuntimeVesion,
            TaskExecutionContext executionContext, 
            string parentAssembly)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, System.Reflection.ProcessorArchitecture.None, false, executionContext)
        {
            this.parentAssembly = parentAssembly;
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

            string directory = FullSearchPath;
            string resolvedPath;

            if (parentAssembly != null)
            {
                var searchLocationsWithParentAssembly = new List<ResolutionSearchLocation>();

                // Resolve to the given path.
                resolvedPath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, directory, searchLocationsWithParentAssembly);

                foreach (var searchLocation in searchLocationsWithParentAssembly)
                {
                    searchLocation.ParentAssembly = parentAssembly;
                }

                assembliesConsideredAndRejected.AddRange(searchLocationsWithParentAssembly);
            }
            else
            {
                resolvedPath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, directory, assembliesConsideredAndRejected);
            }

            if (resolvedPath != null)
            {
                foundPath = resolvedPath;
                return true;
            }

            return false;
        }
    }
}
