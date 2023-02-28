// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Shared;

#nullable disable

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
        public HintPathResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false)
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
        public override bool Resolve(
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
            out bool userRequestedSpecificFile)
        {
            // If there is newline or white space `FileUtilities.NormalizePath` will get garbage result(throw on fullframework).
            // Adding FileUtilities.NormalizePath (https://github.com/dotnet/msbuild/pull/4414) caused https://github.com/dotnet/msbuild/issues/4593
            // It is fixed by adding skip when the path is not valid
            // However, we should consider Trim() the hintpath https://github.com/dotnet/msbuild/issues/4603
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
