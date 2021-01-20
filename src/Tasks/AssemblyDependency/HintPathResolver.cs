// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {HintPathFromItem}
    /// </summary>
    internal class HintPathResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        public HintPathResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, FileExistsInDirectory fileExistsInDirectory, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, fileExistsInDirectory, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false)
        {
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
            List<ResolutionSearchLocation> assembliesConsideredAndRejected,
            out string foundPath,
            out bool userRequestedSpecificFile
        )
        {
            // If there is newline or white space `FileUtilities.NormalizePath` will get garbage result(throw on fullframework).
            // Adding FileUtilities.NormalizePath (https://github.com/microsoft/msbuild/pull/4414) caused https://github.com/microsoft/msbuild/issues/4593
            // It is fixed by adding skip when the path is not valid
            // However, we should consider Trim() the hintpath https://github.com/microsoft/msbuild/issues/4603
            if (!string.IsNullOrEmpty(hintPath) && !FileUtilities.PathIsInvalid(hintPath))
            {
                if (ResolveAsFile(FileUtilities.NormalizePath(hintPath), assemblyName, isPrimaryProjectReference, wantSpecificVersion, true, assembliesConsideredAndRejected))
                {
                    userRequestedSpecificFile = true;
                    foundPath = hintPath;
                    return true;
                }
            }

            foundPath = null;
            userRequestedSpecificFile = false;
            return false;
        }
    }
}
