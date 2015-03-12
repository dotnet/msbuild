// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {GAC}
    /// </summary>
    internal class GacResolver : Resolver
    {
        /// <summary>
        /// Build engine
        /// </summary>
        private IBuildEngine4 _buildEngine;

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64, the processor architecture being targetted.</param>
        public GacResolver(System.Reflection.ProcessorArchitecture targetProcessorArchitecture, string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion, IBuildEngine buildEngine)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, targetProcessorArchitecture, true)
        {
            _buildEngine = buildEngine as IBuildEngine4;
        }


        /// <summary>
        /// Resolve a reference to a specific file name.
        /// </summary>
        /// <param name="assemblyName">The assemblyname of the reference.</param>
        /// <param name="rawFileNameCandidate">The reference's 'include' treated as a raw file name.</param>
        /// <param name="isPrimaryProjectReference">Whether or not this reference was directly from the project file (and therefore not a dependency)</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">Allowed executable extensions.</param>
        /// <param name="hintPath">The item's hintpath value.</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the <AssemblyFolderKey> on the reference in the project.</param>
        /// <param name="candidateAssemblyFiles">List of literal assembly file names to be considered when SearchPaths has {CandidateAssemblyFiles}.</param>
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
                // {GAC} was passed in.
                string gacResolved = GlobalAssemblyCache.GetLocation(_buildEngine, assemblyName, targetProcessorArchitecture, getRuntimeVersion, targetedRuntimeVersion, false /*may not be a fusion name*/, fileExists, null /*Use default delegate in method*/, null /*Use default delegate in method*/, wantSpecificVersion);
                if (gacResolved != null && gacResolved.Length > 0 && fileExists(gacResolved))
                {
                    foundPath = gacResolved;
                    return true;
                }
                else
                {
                    // Record this as a location that was considered.
                    if (assembliesConsideredAndRejected != null)
                    {
                        ResolutionSearchLocation considered = new ResolutionSearchLocation();
                        considered.FileNameAttempted = assemblyName.FullName;
                        considered.SearchPath = searchPathElement;
                        considered.AssemblyName = assemblyName;
                        considered.Reason = NoMatchReason.NotInGac;
                        assembliesConsideredAndRejected.Add(considered);
                    }
                }
            }


            return false;
        }
    }
}
