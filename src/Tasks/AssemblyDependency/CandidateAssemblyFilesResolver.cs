// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {CandidateAssemblyFiles}
    /// </summary>
    internal class CandidateAssemblyFilesResolver : Resolver
    {
        /// <summary>
        /// The candidate assembly files.
        /// </summary>
        private readonly string[] _candidateAssemblyFiles;

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="candidateAssemblyFiles">List of literal assembly file names to be considered when SearchPaths has {CandidateAssemblyFiles}.</param>
        /// <param name="searchPathElement"></param>
        /// <param name="getAssemblyName"></param>
        /// <param name="fileExists"></param>
        public CandidateAssemblyFilesResolver(string[] candidateAssemblyFiles, string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false)
        {
            _candidateAssemblyFiles = candidateAssemblyFiles;
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
                // {CandidateAssemblyFiles} was passed in.
                foreach (string candidateAssemblyFile in _candidateAssemblyFiles)
                {
                    // Filter out disallowed extensions. We don't even want to log them.
                    bool allowedExtension = FileUtilities.HasExtension(candidateAssemblyFile, executableExtensions);
                    if (allowedExtension)
                    {
                        // The file has an allowed extension, so give it a shot.
                        bool matched = false;

                        ResolutionSearchLocation considered = null;
                        if (assembliesConsideredAndRejected != null)
                        {
                            considered = new ResolutionSearchLocation
                            {
                                FileNameAttempted = candidateAssemblyFile,
                                SearchPath = searchPathElement
                            };
                        }

                        if (FileMatchesAssemblyName(assemblyName, isPrimaryProjectReference, wantSpecificVersion, false, candidateAssemblyFile, considered))
                        {
                            matched = true;
                        }
                        else
                        {
                            // Record this as a location that was considered.
                            if (assembliesConsideredAndRejected != null)
                            {
                                Debug.Assert(considered.Reason != NoMatchReason.Unknown, "Expected a no match reason here.");
                                assembliesConsideredAndRejected.Add(considered);
                            }
                        }

                        if (matched)
                        {
                            foundPath = candidateAssemblyFile;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
