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
