// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {RawFileName}
    /// </summary>
    internal class RawFilenameResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        public RawFilenameResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion, TaskEnvironment taskEnvironment)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false, taskEnvironment)
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

            if (rawFileNameCandidate is not null)
            {
                // For empty input, keep the empty string as-is to preserve the pre-MT diagnostic:
                // fileExists("") returns false and the entry is added to assembliesConsideredAndRejected.
                // For non-empty input, absolutize via TaskEnvironment so resolution targets the project
                // directory rather than the process CWD (which is no longer per-project under MT).
                string fullRawFileName = rawFileNameCandidate.Length == 0
                    ? rawFileNameCandidate
                    : taskEnvironment.GetAbsolutePath(rawFileNameCandidate).Value;

                if (isImmutableFrameworkReference || fileExists(fullRawFileName))
                {
                    userRequestedSpecificFile = true;
                    foundPath = fullRawFileName;
                    return true;
                }

                if (assembliesConsideredAndRejected != null)
                {
                    var considered = new ResolutionSearchLocation
                    {
                        FileNameAttempted = fullRawFileName,
                        SearchPath = searchPathElement,
                        Reason = NoMatchReason.NotAFileNameOnDisk
                    };
                    assembliesConsideredAndRejected.Add(considered);
                }
            }

            return false;
        }
    }
}
