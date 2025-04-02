// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Shared;

#nullable disable

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
        /// <param name="searchPathElement">The corresponding element from the search path.</param>
        /// <param name="getAssemblyName">Delegate that gets the assembly name.</param>
        /// <param name="fileExists">Delegate that returns if the file exists.</param>
        /// <param name="getRuntimeVersion">Delegate that returns the clr runtime version for the file.</param>
        /// <param name="targetedRuntimeVesion">The targeted runtime version.</param>
        public CandidateAssemblyFilesResolver(string[] candidateAssemblyFiles, string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false)
        {
            _candidateAssemblyFiles = candidateAssemblyFiles;
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
