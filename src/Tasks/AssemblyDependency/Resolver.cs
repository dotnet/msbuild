﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for all resolver types.
    /// </summary>
    internal abstract class Resolver
    {
        /// <summary>
        /// The corresponding element from the search path.
        /// </summary>
        protected string searchPathElement;

        /// <summary>
        /// Delegate.
        /// </summary>
        protected GetAssemblyName getAssemblyName;

        /// <summary>
        /// Delegate.
        /// </summary>
        protected FileExists fileExists;

        /// <summary>
        /// Delegate
        /// </summary>
        protected GetAssemblyRuntimeVersion getRuntimeVersion;

        /// <summary>
        /// Runtime we are targeting
        /// </summary>
        protected Version targetedRuntimeVersion;

        /// <summary>
        /// Processor architecture we are targeting.
        /// </summary>
        protected ProcessorArchitecture targetProcessorArchitecture;

        /// <summary>
        /// Should the processor architecture we are targeting match the assembly we resolve from disk.
        /// </summary>
        protected bool compareProcessorArchitecture;

        /// <summary>
        /// Construct.
        /// </summary>
        protected Resolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVersion, ProcessorArchitecture targetedProcessorArchitecture, bool compareProcessorArchitecture)
        {
            this.searchPathElement = searchPathElement;
            this.getAssemblyName = getAssemblyName;
            this.fileExists = fileExists;
            this.getRuntimeVersion = getRuntimeVersion;
            this.targetedRuntimeVersion = targetedRuntimeVersion;
            this.targetProcessorArchitecture = targetedProcessorArchitecture;
            this.compareProcessorArchitecture = compareProcessorArchitecture;
        }

        /// <summary>
        /// Resolve a reference to a specific file name.
        /// </summary>
        /// <param name="assemblyName">The assemblyname of the reference.</param>
        /// <param name="sdkName">The name of the sdk to resolve.</param>
        /// <param name="rawFileNameCandidate">The reference's 'include' treated as a raw file name.</param>
        /// <param name="isPrimaryProjectReference">Whether or not this reference was directly from the project file (and therefore not a dependency)</param>
        /// <param name="isImmutableFrameworkReference">True if <paramref name="rawFileNameCandidate"/> is guaranteed to exist on disk and never change.</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">Allowed executable extensions.</param>
        /// <param name="hintPath">The item's hintpath value.</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <param name="foundPath">The path where the file was found.</param>
        /// <param name="userRequestedSpecificFile">Whether or not the user wanted a specific file (for example, HintPath is a request for a specific file)</param>
        /// <returns>True if the file was resolved.</returns>
        public abstract bool Resolve(
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
            out bool userRequestedSpecificFile);

        /// <summary>
        /// The search path element that this resolver is based on.
        /// </summary>
        public string SearchPath => searchPathElement;

        /// <summary>
        /// Resolve a single file.
        /// </summary>
        /// <returns>True if the file was a match, false otherwise.</returns>
        protected bool ResolveAsFile(
            string fullPath,
            AssemblyNameExtension assemblyName,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            bool allowMismatchBetweenFusionNameAndFileName,
            List<ResolutionSearchLocation> assembliesConsideredAndRejected)
        {
            ResolutionSearchLocation considered = null;
            if (assembliesConsideredAndRejected != null)
            {
                considered = new ResolutionSearchLocation
                {
                    FileNameAttempted = fullPath,
                    SearchPath = searchPathElement
                };
            }

            if (FileMatchesAssemblyName(assemblyName, isPrimaryProjectReference, wantSpecificVersion, allowMismatchBetweenFusionNameAndFileName, fullPath, considered))
            {
                return true;
            }

            // Record this as a location that was considered.
            assembliesConsideredAndRejected?.Add(considered);

            return false;
        }

        /// <summary>
        /// Determines whether an assembly name matches the assembly pointed to by pathToCandidateAssembly
        /// </summary>
        /// <param name="assemblyName">The assembly name to look up.</param>
        /// <param name="isPrimaryProjectReference">True if this is a primary reference directly from the project file.</param>
        /// <param name="wantSpecificVersion">Whether the version needs to match exactly or loosely.</param>
        /// <param name="allowMismatchBetweenFusionNameAndFileName">Whether to allow naming mismatch.</param>
        /// <param name="pathToCandidateAssembly">Path to a possible file.</param>
        /// <param name="searchLocation">Information about why the candidate file didn't match</param>
        protected bool FileMatchesAssemblyName(
            AssemblyNameExtension assemblyName,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            bool allowMismatchBetweenFusionNameAndFileName,
            string pathToCandidateAssembly,
            ResolutionSearchLocation searchLocation)
        {
            if (searchLocation != null)
            {
                searchLocation.FileNameAttempted = pathToCandidateAssembly;
            }

            // Base name of the target file has to match the Name from the assemblyName
            if (!allowMismatchBetweenFusionNameAndFileName)
            {
                string candidateBaseName = Path.GetFileNameWithoutExtension(pathToCandidateAssembly);
                if (!String.Equals(assemblyName?.Name, candidateBaseName, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (searchLocation != null)
                    {
                        if (candidateBaseName.Length > 0)
                        {
                            searchLocation.AssemblyName = new AssemblyNameExtension(candidateBaseName);
                            searchLocation.Reason = NoMatchReason.FusionNamesDidNotMatch;
                        }
                        else
                        {
                            searchLocation.Reason = NoMatchReason.TargetHadNoFusionName;
                        }
                    }
                    return false;
                }
            }

            bool isSimpleAssemblyName = assemblyName?.IsSimpleName == true;

            if (fileExists(pathToCandidateAssembly))
            {
                // If the resolver we are using is targeting a given processor architecture then we must crack open the assembly and make sure the architecture is compatible
                // We cannot do these simple name matches.
                if (!compareProcessorArchitecture)
                {
                    // If the file existed and the reference is a simple primary reference which does not contain an assembly name (say a raw file name)
                    // then consider this a match.
                    if (assemblyName == null && isPrimaryProjectReference && !wantSpecificVersion)
                    {
                        return true;
                    }

                    if (isPrimaryProjectReference && !wantSpecificVersion && isSimpleAssemblyName)
                    {
                        return true;
                    }
                }

                // We have strong name information, so do some added verification here.
                AssemblyNameExtension targetAssemblyName = null;
                try
                {
                    targetAssemblyName = getAssemblyName(pathToCandidateAssembly);
                }
                catch (FileLoadException)
                {
                    // Its pretty hard to get here, you need an assembly that contains a valid reference
                    // to a dependent assembly that, in turn, throws a FileLoadException during GetAssemblyName.
                    // Still it happened once, with an older version of the CLR. 

                    // ...falling through and relying on the targetAssemblyName==null behavior below...
                }
                catch (BadImageFormatException)
                {
                    // As above, this is weird: there's a valid reference to an assembly with a file on disk
                    // that isn't a valid .NET assembly. Might be the result of mid-build corruption, but
                    // could just be a name collision on one of the possible resolution paths.

                    // as above, fall through.
                }

                if (searchLocation != null)
                {
                    searchLocation.AssemblyName = targetAssemblyName;
                }

                // targetAssemblyName may be null if there was no metadata for this assembly.
                // In this case, there's no match.
                if (targetAssemblyName != null)
                {
                    // If we are targeting a given processor architecture check to see if they match, if we are targeting MSIL then any architecture will do.
                    if (compareProcessorArchitecture)
                    {
                        // Only reject the assembly if the target processor architecture does not match the assemby processor architecture and the assembly processor architecture is not NONE or MSIL.
                        if (
                              targetAssemblyName.AssemblyName.ProcessorArchitecture != targetProcessorArchitecture &&  /* The target and assembly architectures do not match*/
                              (targetProcessorArchitecture != ProcessorArchitecture.None && targetAssemblyName.AssemblyName.ProcessorArchitecture != ProcessorArchitecture.None)  /*The assembly is not none*/
                              && (targetProcessorArchitecture != ProcessorArchitecture.MSIL && targetAssemblyName.AssemblyName.ProcessorArchitecture != ProcessorArchitecture.MSIL)) /*The assembly is not MSIL*/
                        {
                            if (searchLocation != null)
                            {
                                searchLocation.Reason = NoMatchReason.ProcessorArchitectureDoesNotMatch;
                            }
                            return false;
                        }
                    }

                    bool matchedSpecificVersion = (wantSpecificVersion && assemblyName?.Equals(targetAssemblyName) == true);
                    bool matchPartialName = !wantSpecificVersion && assemblyName?.PartialNameCompare(targetAssemblyName) == true;

                    if (matchedSpecificVersion || matchPartialName)
                    {
                        return true;
                    }
                    else
                    {
                        // Reason was: FusionNames did not match.
                        if (searchLocation != null)
                        {
                            searchLocation.Reason = NoMatchReason.FusionNamesDidNotMatch;
                        }
                    }
                }
                else
                {
                    // Reason was: Target had no fusion name.
                    if (searchLocation != null)
                    {
                        searchLocation.Reason = NoMatchReason.TargetHadNoFusionName;
                    }
                }
            }
            else
            {
                // Reason was: No file found at that location.
                if (searchLocation != null)
                {
                    searchLocation.Reason = NoMatchReason.FileNotFound;
                }
            }

            return false;
        }

        /// <summary>
        /// Given a strong name, which may optionally have Name, Version and Public Key,
        /// return a fully qualified directory name.
        /// </summary>
        /// <param name="assemblyName">The assembly name to look up.</param>
        /// <param name="isPrimaryProjectReference">True if this is a primary reference directly from the project file.</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">The possible filename extensions of the assembly. Must be one of these or its no match.</param>
        /// <param name="directory">the directory to look in</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <returns>'null' if the assembly wasn't found.</returns>
        protected string ResolveFromDirectory(
            AssemblyNameExtension assemblyName,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string directory,
            List<ResolutionSearchLocation> assembliesConsideredAndRejected)
        {
            if (assemblyName == null)
            {
                // This can happen if the assembly name is actually a file name.
                return null;
            }

            // used for the case when we are targeting MSIL and need to return that if it exists. This is different from targeting other architectures where returning an MSIL or target architecture are ok.
            string candidateFullPath = null;

            if (directory != null)
            {
                string weakNameBase = assemblyName.Name;
                foreach (string executableExtension in executableExtensions)
                {
                    string baseName = weakNameBase + executableExtension;
                    string fullPath;

                    try
                    {
                        fullPath = Path.Combine(directory, baseName);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        // Assuming it's the search path that's bad. But combine them both so the error is visible if it's the reference itself.
                        throw new InvalidParameterValueException("SearchPaths", directory + (directory.EndsWith("\\", StringComparison.OrdinalIgnoreCase) ? String.Empty : "\\") + baseName, e.Message);
                    }

                    // We have a full path returned 
                    if (ResolveAsFile(fullPath, assemblyName, isPrimaryProjectReference, wantSpecificVersion, false, assembliesConsideredAndRejected))
                    {
                        if (candidateFullPath == null)
                        {
                            candidateFullPath = fullPath;
                        }

                        /*
                         * After finding a file we now will check to see if it matches the type of processor architecture we want to return. The rules are as follows
                         * 
                         * If targeting AMD64 / X86 / IA64 / ARM /NONE we will return the first assembly which has a matching processor architecture OR is an assembly with a processor architecture of MSIL or NONE
                         * 
                         * If targeting MSIL we will first look through all of the assemblies, if an MSIL assembly is found we will return that. If no MSIL assembly is found we will return 
                         * the first assembly which matches reguardless of its processor architecture.
                         */

                        if (targetProcessorArchitecture == ProcessorArchitecture.MSIL)
                        {
                            // Lets see if the processor architecture matches
                            AssemblyNameExtension foundAssembly = getAssemblyName(fullPath);

                            // If the processor architecture does not match the we should continue to see if there is a better match.
                            if (foundAssembly?.AssemblyName.ProcessorArchitecture == ProcessorArchitecture.MSIL)
                            {
                                return fullPath;
                            }
                        }
                        else
                        {
                            return fullPath;
                        }
                    }
                }

                // If we did not find an assembly that matched then see if the assembly name is actually a filename.
                if (candidateFullPath == null)
                {
                    // If the file ends with an extension like .dll or .exe then just try that.
                    string weakNameBaseExtension = Path.GetExtension(weakNameBase);
                    string weakNameBaseFileName = Path.GetFileNameWithoutExtension(weakNameBase);

                    if (!string.IsNullOrEmpty(weakNameBaseExtension) && !string.IsNullOrEmpty(weakNameBaseFileName))
                    {
                        foreach (string executableExtension in executableExtensions)
                        {
                            if (String.Equals(executableExtension, weakNameBaseExtension, StringComparison.CurrentCultureIgnoreCase))
                            {
                                string fullPath = Path.Combine(directory, weakNameBase);
                                var extensionlessAssemblyName = new AssemblyNameExtension(weakNameBaseFileName);

                                if (ResolveAsFile(fullPath, extensionlessAssemblyName, isPrimaryProjectReference, wantSpecificVersion, false, assembliesConsideredAndRejected))
                                {
                                    return fullPath;
                                }
                            }
                        }
                    }
                }
            }

            return candidateFullPath;
        }
    }
}
