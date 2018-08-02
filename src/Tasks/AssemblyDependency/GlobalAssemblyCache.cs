// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.Framework;
using System.Collections.Concurrent;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Methods for dealing with the GAC.
    /// </summary>
    internal static class GlobalAssemblyCache
    {
        /// <summary>
        /// Default delegate to get the path based on a fusion name.
        /// </summary>
        internal static readonly GetPathFromFusionName pathFromFusionName = RetrievePathFromFusionName;

        /// <summary>
        /// Default delegate to get the gac enumerator.
        /// </summary>
        internal static readonly GetGacEnumerator gacEnumerator = GetGacNativeEnumerator;

        /// <summary>
        /// Given a strong name, find its path in the GAC.
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64.</param>
        /// <returns>The path to the assembly. Empty if none exists.</returns>
        private static string GetLocationImpl(AssemblyNameExtension assemblyName, string targetProcessorArchitecture, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntime, FileExists fileExists, GetPathFromFusionName getPathFromFusionName, GetGacEnumerator getGacEnumerator, bool specificVersion)
        {
            // Extra checks for PInvoke-destined data.
            ErrorUtilities.VerifyThrowArgumentNull(assemblyName, nameof(assemblyName));
            ErrorUtilities.VerifyThrow(assemblyName.FullName != null, "Got a null assembly name fullname.");

            string strongName = assemblyName.FullName;

            if (targetProcessorArchitecture != null && !assemblyName.HasProcessorArchitectureInFusionName)
            {
                strongName += ", ProcessorArchitecture=" + targetProcessorArchitecture;
            }

            string assemblyPath = String.Empty;

            // Dictionary sorted by Version in reverse order, this will give the values enumeration the highest runtime version first.
            SortedDictionary<Version, SortedDictionary<AssemblyNameExtension, string>> assembliesByRuntime = GenerateListOfAssembliesByRuntime(strongName, getRuntimeVersion, targetedRuntime, fileExists, getPathFromFusionName, getGacEnumerator, specificVersion);
            if (assembliesByRuntime != null)
            {
                foreach (SortedDictionary<AssemblyNameExtension, string> runtimeBucket in assembliesByRuntime.Values)
                {
                    // Grab the first element if there are one or more elements. This will give us the highest version assembly name.
                    if (runtimeBucket.Count > 0)
                    {
                        foreach (KeyValuePair<AssemblyNameExtension, string> kvp in runtimeBucket)
                        {
                            assemblyPath = kvp.Value;
                            break;
                        }

                        if (!String.IsNullOrEmpty(assemblyPath))
                        {
                            break;
                        }
                    }
                }
            }

            return assemblyPath;
        }

        /// <summary>
        /// Given a strong name generate the gac enumerator.
        /// </summary>
        internal static IEnumerable<AssemblyNameExtension> GetGacNativeEnumerator(string strongName)
        {
            try
            {
                // Will fail if the publickeyToken is null but will not fail if it is missing.
                return new NativeMethods.AssemblyCacheEnum(strongName);
            }
            catch (FileLoadException)
            {
                // We could not handle the name passed in
                return null;
            }
        }

        /// <summary>
        /// Enumerate the gac and generate a list of assemblies which match the strongname by runtime.
        /// </summary>
        private static SortedDictionary<Version, SortedDictionary<AssemblyNameExtension, string>> GenerateListOfAssembliesByRuntime(string strongName, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntime, FileExists fileExists, GetPathFromFusionName getPathFromFusionName, GetGacEnumerator getGacEnumerator, bool specificVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(targetedRuntime, nameof(targetedRuntime));

            IEnumerable<AssemblyNameExtension> gacEnum = getGacEnumerator(strongName);

            // Dictionary of Runtime version (sorted in reverse order) to a list of assemblies which are part of that runtime. This will allow us to pick the highest runtime and version first.
            SortedDictionary<Version, SortedDictionary<AssemblyNameExtension, string>> assembliesWithValidRuntimes = new SortedDictionary<Version, SortedDictionary<AssemblyNameExtension, string>>(ReverseVersionGenericComparer.Comparer);

            // Enumerate the gac values returned based on the partial or full fusion name.
            if (gacEnum != null)
            {
                foreach (AssemblyNameExtension gacAssembly in gacEnum)
                {
                    // We only have a fusion name from the IAssemblyName interface we need to get the path to the assembly to resolve it and to check its runtime.
                    string assemblyPath = getPathFromFusionName(gacAssembly.FullName);

                    // Make sure we could get the path from the Fusion name and make sure the file actually exists.
                    if (!String.IsNullOrEmpty(assemblyPath) && fileExists(assemblyPath))
                    {
                        // Get the runtime version from the found assembly.
                        string runtimeVersionRaw = getRuntimeVersion(assemblyPath);

                        // Convert the runtime string to a version so we can properly compare them as per version object comparison rules. 
                        // We will accept version which are less than or equal to the targeted runtime.
                        Version runtimeVersion = VersionUtilities.ConvertToVersion(runtimeVersionRaw);

                        // Make sure the targeted runtime is greater than or equal to the runtime version of the assembly we got from the gac.
                        if (runtimeVersion != null)
                        {
                            if (targetedRuntime.CompareTo(runtimeVersion) >= 0 || specificVersion)
                            {
                                SortedDictionary<AssemblyNameExtension, string> assembliesWithRuntime = null;
                                assembliesWithValidRuntimes.TryGetValue(runtimeVersion, out assembliesWithRuntime);

                                // Create a new list if one does not exist.
                                if (assembliesWithRuntime == null)
                                {
                                    assembliesWithRuntime = new SortedDictionary<AssemblyNameExtension, string>(AssemblyNameReverseVersionComparer.GenericComparer);
                                    assembliesWithValidRuntimes.Add(runtimeVersion, assembliesWithRuntime);
                                }

                                if (!assembliesWithRuntime.ContainsKey(gacAssembly))
                                {
                                    // Add the assembly to the list
                                    assembliesWithRuntime.Add(gacAssembly, assemblyPath);
                                }
                            }
                        }
                    }
                }
            }

            return assembliesWithValidRuntimes;
        }

        /// <summary>
        /// Given a fusion name get the path to the assembly on disk.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Build.Tasks.IAssemblyCache.QueryAssemblyInfo(System.UInt32,System.String,Microsoft.Build.Tasks.ASSEMBLY_INFO@)", Justification = "We use the out parameters to determine if we got a good assembly back or not")]
        internal static string RetrievePathFromFusionName(string strongName)
        {
            // Extra checks for PInvoke-destined data.
            ErrorUtilities.VerifyThrowArgumentNull(strongName, nameof(strongName));

            string value;

            if (NativeMethodsShared.IsWindows)
            {
                uint hr = NativeMethods.CreateAssemblyCache(out IAssemblyCache assemblyCache, 0);

                ErrorUtilities.VerifyThrow(hr == NativeMethodsShared.S_OK, "CreateAssemblyCache failed, hr {0}", hr);

                var assemblyInfo = new ASSEMBLY_INFO { cbAssemblyInfo = (uint) Marshal.SizeOf<ASSEMBLY_INFO>() };

                assemblyCache.QueryAssemblyInfo(0, strongName, ref assemblyInfo);

                if (assemblyInfo.cbAssemblyInfo == 0) return null;

                assemblyInfo.pszCurrentAssemblyPathBuf = new string(new char[assemblyInfo.cchBuf]);

                assemblyCache.QueryAssemblyInfo(0, strongName, ref assemblyInfo);

                value = assemblyInfo.pszCurrentAssemblyPathBuf;
            }
            else
            {
                value = NativeMethods.AssemblyCacheEnum.AssemblyPathFromStrongName(strongName);
            }

            return value;
        }

        /// <summary>
        /// If we know we have a full fusion name we can skip enumerating the gac and just query for the path. This will 
        /// not check the runtime version of the assembly.
        /// </summary>
        private static string CheckForFullFusionNameInGac(AssemblyNameExtension assemblyName, string targetProcessorArchitecture, GetPathFromFusionName getPathFromFusionName)
        {
            string strongName = assemblyName.FullName;
            if (targetProcessorArchitecture != null && !assemblyName.HasProcessorArchitectureInFusionName)
            {
                strongName += ", ProcessorArchitecture=" + targetProcessorArchitecture;
            }

            return getPathFromFusionName(strongName);
        }

        /// <summary>
        /// Given a strong name, find its path in the GAC.
        /// </summary>
        /// <param name="strongName">The strong name.</param>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64.</param>
        /// <param name="getRuntimeVersion">Delegate to get the runtime version from a file path</param>
        /// <param name="targetedRuntimeVersion">What version of the runtime are we targeting</param>
        /// <param name="fullFusionName">Are we guranteed to have a full fusion name. This really can only happen if we have already resolved the assembly</param>
        /// <returns>The path to the assembly. Empty if none exists.</returns>
        internal static string GetLocation
        (
            AssemblyNameExtension strongName,
            ProcessorArchitecture targetProcessorArchitecture,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Version targetedRuntimeVersion,
            bool fullFusionName,
            FileExists fileExists,
            GetPathFromFusionName getPathFromFusionName,
            GetGacEnumerator getGacEnumerator,
            bool specificVersion
        )
        {
            return GetLocation(null, strongName, targetProcessorArchitecture, getRuntimeVersion, targetedRuntimeVersion, fullFusionName, fileExists, getPathFromFusionName, getGacEnumerator, specificVersion);
        }

        /// <summary>
        /// Given a strong name, find its path in the GAC.
        /// </summary>
        /// <param name="strongName">The strong name.</param>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64.</param>
        /// <param name="getRuntimeVersion">Delegate to get the runtime version from a file path</param>
        /// <param name="targetedRuntimeVersion">What version of the runtime are we targeting</param>
        /// <param name="fullFusionName">Are we guranteed to have a full fusion name. This really can only happen if we have already resolved the assembly</param>
        /// <returns>The path to the assembly. Empty if none exists.</returns>
        internal static string GetLocation
        (
            IBuildEngine4 buildEngine,
            AssemblyNameExtension strongName,
            ProcessorArchitecture targetProcessorArchitecture,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Version targetedRuntimeVersion,
            bool fullFusionName,
            FileExists fileExists,
            GetPathFromFusionName getPathFromFusionName,
            GetGacEnumerator getGacEnumerator,
            bool specificVersion
        )
        {
            ConcurrentDictionary<AssemblyNameExtension, string> fusionNameToResolvedPath = null;
            bool useGacRarCache = Environment.GetEnvironmentVariable("MSBUILDDISABLEGACRARCACHE") == null;
            if (buildEngine != null && useGacRarCache)
            {
                string key = "44d78b60-3bbe-48fe-9493-04119ebf515f" + "|" + targetProcessorArchitecture.ToString() + "|" + targetedRuntimeVersion.ToString() + "|" + fullFusionName.ToString() + "|" + specificVersion.ToString();
                fusionNameToResolvedPath = buildEngine.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build) as ConcurrentDictionary<AssemblyNameExtension, string>;
                if (fusionNameToResolvedPath == null)
                {
                    fusionNameToResolvedPath = new ConcurrentDictionary<AssemblyNameExtension, string>(AssemblyNameComparer.GenericComparer);
                    buildEngine.RegisterTaskObject(key, fusionNameToResolvedPath, RegisteredTaskObjectLifetime.Build, true /* dispose early ok*/);
                }
                else
                {
                    if (fusionNameToResolvedPath.ContainsKey(strongName))
                    {
                        fusionNameToResolvedPath.TryGetValue(strongName, out string fusionName);
                        return fusionName;
                    }
                }
            }

            // Optimize out the case where the public key token is null, if it is null it is not a strongly named assembly and CANNOT be in the gac.
            // also passing it would cause the gac enumeration method to throw an exception indicating the assembly is not a strongnamed assembly.

            // If the publickeyToken is null and the publickeytoken is in the fusion name then this means we are passing in a null or empty PublicKeyToken and then this cannot possibly be in the gac.
            if ((strongName.GetPublicKeyToken() == null || strongName.GetPublicKeyToken().Length == 0) && strongName.FullName.IndexOf("PublicKeyToken", StringComparison.OrdinalIgnoreCase) != -1)
            {
                fusionNameToResolvedPath?.TryAdd(strongName, null);
                return null;
            }

            // A delegate was not passed in to use the default one
            getPathFromFusionName = getPathFromFusionName ?? pathFromFusionName;

            // A delegate was not passed in to use the default one
            getGacEnumerator = getGacEnumerator ?? gacEnumerator;

            // If we have no processor architecture set then we can tryout a number of processor architectures.
            string location;
            if (!strongName.HasProcessorArchitectureInFusionName)
            {
                if (targetProcessorArchitecture != ProcessorArchitecture.MSIL && targetProcessorArchitecture != ProcessorArchitecture.None)
                {
                    string processorArchitecture = ResolveAssemblyReference.ProcessorArchitectureToString(targetProcessorArchitecture);
                    // Try processor specific first.
                    if (fullFusionName)
                    {
                        location = CheckForFullFusionNameInGac(strongName, processorArchitecture, getPathFromFusionName);
                    }
                    else
                    {
                        location = GetLocationImpl(strongName, processorArchitecture, getRuntimeVersion, targetedRuntimeVersion, fileExists, getPathFromFusionName, getGacEnumerator, specificVersion);
                    }

                    if (!string.IsNullOrEmpty(location))
                    {
                        fusionNameToResolvedPath?.TryAdd(strongName, location);
                        return location;
                    }
                }

                // Next, try MSIL
                if (fullFusionName)
                {
                    location = CheckForFullFusionNameInGac(strongName, "MSIL", getPathFromFusionName);
                }
                else
                {
                    location = GetLocationImpl(strongName, "MSIL", getRuntimeVersion, targetedRuntimeVersion, fileExists, getPathFromFusionName, getGacEnumerator, specificVersion);
                }
                if (!string.IsNullOrEmpty(location))
                {
                    fusionNameToResolvedPath?.TryAdd(strongName, location);
                    return location;
                }
            }

            // Next, try no processor architecure
            if (fullFusionName)
            {
                location = CheckForFullFusionNameInGac(strongName, null, getPathFromFusionName);
            }
            else
            {
                location = GetLocationImpl(strongName, null, getRuntimeVersion, targetedRuntimeVersion, fileExists, getPathFromFusionName, getGacEnumerator, specificVersion);
            }

            if (!string.IsNullOrEmpty(location))
            {
                fusionNameToResolvedPath?.TryAdd(strongName, location);
                return location;
            }

            fusionNameToResolvedPath?.TryAdd(strongName, null);

            return null;
        }

        /// <summary>
        /// Return the root path of the GAC
        /// </summary>
        internal static string GetGacPath()
        {
            int gacPathLength = 0;
            NativeMethods.GetCachePath(AssemblyCacheFlags.GAC, null, ref gacPathLength);
            StringBuilder gacPath = new StringBuilder(gacPathLength);
            NativeMethods.GetCachePath(AssemblyCacheFlags.GAC, gacPath, ref gacPathLength);

            return gacPath.ToString();
        }
    }
}
