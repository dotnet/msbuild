// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyFoldersFromConfig;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Utility class encapsulates steps to resolve assembly references.
    /// For example, this class has the code that will take:
    ///
    ///      System.Xml
    ///
    /// and turn it into:
    ///
    ///     [path-to-frameworks]\System.Xml.dll
    ///
    /// 
    /// </summary>
    internal static class AssemblyResolution
    {
        /// <summary>
        /// Implementation guts for ResolveReference.
        /// </summary>
        /// <param name="jaggedResolvers">The array of resolvers to search with.</param>
        /// <param name="assemblyName">The assembly name to look up.</param>
        /// <param name="sdkName"></param>
        /// <param name="rawFileNameCandidate">The file name to match if {RawFileName} is seen. (May be null).</param>
        /// <param name="isPrimaryProjectReference">True if this is a primary reference directly from the project file.</param>
        /// <param name="wantSpecificVersion"></param>
        /// <param name="executableExtensions">The filename extension of the assembly. Must be this or its no match.</param>
        /// <param name="hintPath">This reference's hintpath</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <param name="resolvedSearchPath">Receives the searchPath that the reference was resolved at. Empty if not resolved.</param>
        /// <param name="userRequestedSpecificFile"> This will be true if the user requested a specific file.</param>
        /// <returns>The resolved path</returns>
        internal static string ResolveReference
        (
            IEnumerable<Resolver[]> jaggedResolvers,
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            ArrayList assembliesConsideredAndRejected,
            out string resolvedSearchPath,
            out bool userRequestedSpecificFile
        )
        {
            // Initialize outs.
            userRequestedSpecificFile = false;
            resolvedSearchPath = String.Empty;

            // Search each group of resolvers
            foreach (Resolver[] resolvers in jaggedResolvers)
            {
                // Tolerate null resolvers.
                if (resolvers == null)
                {
                    break;
                }

                // Search each searchpath.
                foreach (Resolver resolver in resolvers)
                {
                    if
                    (
                        resolver.Resolve
                        (
                            assemblyName,
                            sdkName,
                            rawFileNameCandidate,
                            isPrimaryProjectReference,
                            wantSpecificVersion,
                            executableExtensions,
                            hintPath,
                            assemblyFolderKey,
                            assembliesConsideredAndRejected,
                            out string fileLocation,
                            out userRequestedSpecificFile
                        )
                    )
                    {
                        resolvedSearchPath = resolver.SearchPath;
                        return fileLocation;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Compile search paths into an array of resolvers.
        /// </summary>
        /// <param name="buildEngine"></param>
        /// <param name="searchPaths"></param>
        /// <param name="candidateAssemblyFiles">Paths to assembly files mentioned in the project.</param>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64, the processor architecture being targetted.</param>
        /// <param name="frameworkPaths">Paths to FX folders.</param>
        /// <param name="fileExists"></param>
        /// <param name="getAssemblyName"></param>
        /// <param name="getRegistrySubKeyNames"></param>
        /// <param name="getRegistrySubKeyDefaultValue"></param>
        /// <param name="openBaseKey"></param>
        /// <param name="installedAssemblies"></param>
        /// <param name="getRuntimeVersion"></param>
        /// <param name="targetedRuntimeVersion"></param>
        /// <param name="getAssemblyPathInGac"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static Resolver[] CompileSearchPaths
        (
            IBuildEngine buildEngine,
            string[] searchPaths,
            string[] candidateAssemblyFiles,
            System.Reflection.ProcessorArchitecture targetProcessorArchitecture,
            string[] frameworkPaths,
            FileExists fileExists,
            GetAssemblyName getAssemblyName,
#if FEATURE_WIN32_REGISTRY
            GetRegistrySubKeyNames getRegistrySubKeyNames,
            GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue,
            OpenBaseKey openBaseKey,
#endif
            InstalledAssemblies installedAssemblies,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Version targetedRuntimeVersion,
            GetAssemblyPathInGac getAssemblyPathInGac,
            TaskLoggingHelper log
        )
        {
            var resolvers = new Resolver[searchPaths.Length];

            for (int p = 0; p < searchPaths.Length; ++p)
            {
                string basePath = searchPaths[p];

                // Was {HintPathFromItem} specified? If so, take the Item's
                // HintPath property.
                if (0 == String.Compare(basePath, AssemblyResolutionConstants.hintPathSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new HintPathResolver(searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
                else if (0 == String.Compare(basePath, AssemblyResolutionConstants.frameworkPathSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new FrameworkPathResolver(frameworkPaths, installedAssemblies, searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
                else if (0 == String.Compare(basePath, AssemblyResolutionConstants.rawFileNameSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new RawFilenameResolver(searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
                else if (0 == String.Compare(basePath, AssemblyResolutionConstants.candidateAssemblyFilesSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new CandidateAssemblyFilesResolver(candidateAssemblyFiles, searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
#if FEATURE_GAC
                else if (0 == String.Compare(basePath, AssemblyResolutionConstants.gacSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new GacResolver(targetProcessorArchitecture, searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion, getAssemblyPathInGac);
                }
#endif
                else if (0 == String.Compare(basePath, AssemblyResolutionConstants.assemblyFoldersSentinel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new AssemblyFoldersResolver(searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
#if FEATURE_WIN32_REGISTRY
                // Check for AssemblyFoldersEx sentinel.
                else if (0 == String.Compare(basePath, 0, AssemblyResolutionConstants.assemblyFoldersExSentinel, 0, AssemblyResolutionConstants.assemblyFoldersExSentinel.Length, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new AssemblyFoldersExResolver(searchPaths[p], getAssemblyName, fileExists, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getRuntimeVersion, openBaseKey, targetedRuntimeVersion, targetProcessorArchitecture, true, buildEngine);
                }
#endif
                else if (0 == String.Compare(basePath, 0, AssemblyResolutionConstants.assemblyFoldersFromConfigSentinel, 0, AssemblyResolutionConstants.assemblyFoldersFromConfigSentinel.Length, StringComparison.OrdinalIgnoreCase))
                {
                    resolvers[p] = new AssemblyFoldersFromConfigResolver(searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion, targetProcessorArchitecture, true, buildEngine, log);
                }
                else
                {
                    resolvers[p] = new DirectoryResolver(searchPaths[p], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
                }
            }
            return resolvers;
        }

        /// <summary>
        /// Build a resolver array from a set of directories to resolve directly from.
        /// </summary>
        internal static Resolver[] CompileDirectories
        (
            List<string> directories,
            FileExists fileExists,
            GetAssemblyName getAssemblyName,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Version targetedRuntimeVersion
        )
        {
            var resolvers = new Resolver[directories.Count];
            for (int i = 0; i < directories.Count; i++)
            {
                resolvers[i] = new DirectoryResolver(directories[i], getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVersion);
            }

            return resolvers;
        }
    }
}
