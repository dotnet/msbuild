// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Get the reference assembly paths for a given target framework version / moniker.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the reference assembly paths to the various frameworks
    /// </summary>
    public class GetReferenceAssemblyPaths : TaskExtension
    {
        #region Data
#if FEATURE_GAC
        /// <summary>
        /// This is the sentinel assembly for .NET FX 3.5 SP1
        /// Used to determine if SP1 of 3.5 is installed
        /// </summary>
        private const string NET35SP1SentinelAssemblyName = "System.Data.Entity, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, processorArchitecture=MSIL";

        /// <summary>
        /// Cache in a static whether or not we have found the 35sp1sentinel assembly.
        /// </summary>
        private static bool? s_net35SP1SentinelAssemblyFound;
#endif

        /// <summary>
        /// Hold the reference assembly paths based on the passed in targetframeworkmoniker.
        /// </summary>
        private IList<string> _tfmPaths;

        /// <summary>
        /// Hold the reference assembly paths based on the passed in targetframeworkmoniker without considering any profile passed in.
        /// </summary>
        private IList<string> _tfmPathsNoProfile;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the path based on the passed in TargetFrameworkMoniker. If the TargetFrameworkMoniker is null or empty
        /// this path will be empty.
        /// </summary>
        [Output]
        public string[] ReferenceAssemblyPaths
        {
            get
            {
                if (_tfmPaths != null)
                {
                    var pathsToReturn = new string[_tfmPaths.Count];
                    _tfmPaths.CopyTo(pathsToReturn, 0);
                    return pathsToReturn;
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
        }

        /// <summary>
        /// Returns the path based on the passed in TargetFrameworkMoniker without considering the profile part of the moniker. If the TargetFrameworkMoniker is null or empty
        /// this path will be empty.
        /// </summary>
        [Output]
        public string[] FullFrameworkReferenceAssemblyPaths
        {
            get
            {
                if (_tfmPathsNoProfile != null)
                {
                    string[] pathsToReturn = new string[_tfmPathsNoProfile.Count];
                    _tfmPathsNoProfile.CopyTo(pathsToReturn, 0);
                    return pathsToReturn;
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
        }

        /// <summary>
        /// The target framework moniker to get the reference assembly paths for
        /// </summary>
        public string TargetFrameworkMoniker { get; set; }

        /// <summary>
        /// The root path to use to generate the reference assembly path
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// By default GetReferenceAssemblyPaths performs simple checks
        /// to ensure that certain runtime frameworks are installed depending on the
        /// target framework.
        /// set BypassFrameworkInstallChecks to true in order to bypass those checks.
        /// </summary>        
        public bool BypassFrameworkInstallChecks { get; set; }

        /// <summary>
        /// If set to true, the task will not generate an error (or a warning) if the reference assemblies cannot be found.
        /// This allows the task to be used to check whether reference assemblies for a framework are available.
        /// </summary>
        public bool SuppressNotFoundError { get; set; }

        /// <summary>
        /// Gets the display name for the targetframeworkmoniker
        /// </summary>
        [Output]
        public string TargetFrameworkMonikerDisplayName { get; set; }

        /// <summary>
        /// Target frameworks are looked up in @RootPath. If it cannot be found
        /// there, then paths in @TargetFrameworkFallbackSearchPaths
        /// are used for the lookup, in order. This can have multiple paths, separated
        /// by ';'
        /// </summary>
        public string TargetFrameworkFallbackSearchPaths
        {
            get;
            set;
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// If the target framework moniker is set, generate the correct Paths.
        /// </summary>
        public override bool Execute()
        {
            FrameworkNameVersioning moniker;
            FrameworkNameVersioning monikerWithNoProfile = null;

            // Are we targeting a profile. 
            bool targetingProfile;

            try
            {
                moniker = new FrameworkNameVersioning(TargetFrameworkMoniker);
                targetingProfile = !String.IsNullOrEmpty(moniker.Profile);

                // If we are targeting a profile we need to generate a set of reference assembly paths which describe where the full framework 
                //  exists, to do so we need to get the reference assembly location without the profile as part of the moniker.
                if (targetingProfile)
                {
                    monikerWithNoProfile = new FrameworkNameVersioning(moniker.Identifier, moniker.Version);
                }

#if FEATURE_GAC
                // This is a very specific "hack" to ensure that when we're targeting certain .NET Framework versions that
                // WPF gets to rely on .NET FX 3.5 SP1 being installed on the build machine.
                // This only needs to occur when we are targeting a .NET FX prior to v4.0
                if (!BypassFrameworkInstallChecks && moniker.Identifier.Equals(".NETFramework", StringComparison.OrdinalIgnoreCase) &&
                    moniker.Version.Major < 4)
                {
                    // We have not got a value for whether or not the 35 sentinel assembly has been found
                    if (!s_net35SP1SentinelAssemblyFound.HasValue)
                    {
                        // get an assemblyname from the string representation of the sentinel assembly name
                        var sentinelAssemblyName = new AssemblyNameExtension(NET35SP1SentinelAssemblyName);

                        string path = GlobalAssemblyCache.GetLocation(sentinelAssemblyName, SystemProcessorArchitecture.MSIL, runtimeVersion => "v2.0.50727", new Version("2.0.57027"), false, new FileExists(FileUtilities.FileExistsNoThrow), GlobalAssemblyCache.pathFromFusionName, GlobalAssemblyCache.gacEnumerator, false);
                        s_net35SP1SentinelAssemblyFound = !String.IsNullOrEmpty(path);
                    }

                    // We did not find the SP1 sentinel assembly in the GAC. Therefore we must assume that SP1 isn't installed
                    if (!s_net35SP1SentinelAssemblyFound.Value)
                    {
                        Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.NETFX35SP1NotIntstalled", TargetFrameworkMoniker);
                    }
                }
#endif
            }
            catch (ArgumentException e)
            {
                Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.InvalidTargetFrameworkMoniker", TargetFrameworkMoniker, e.Message);
                return false;
            }

            try
            {
                _tfmPaths = GetPaths(RootPath, TargetFrameworkFallbackSearchPaths, moniker);

                if (_tfmPaths != null && _tfmPaths.Count > 0)
                {
                    TargetFrameworkMonikerDisplayName = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(_tfmPaths[0], moniker);
                }

                // If there is a profile get the paths without the profile.
                // There is no point in generating the full framework paths if profile path could not be found.
                if (targetingProfile && _tfmPaths != null)
                {
                    _tfmPathsNoProfile = GetPaths(RootPath, TargetFrameworkFallbackSearchPaths, monikerWithNoProfile);
                }

                // The path with out the profile is just the reference assembly paths.
                if (!targetingProfile)
                {
                    _tfmPathsNoProfile = _tfmPaths;
                }
            }
            catch (Exception e)
            {
                // The reason we need to do exception E here is because we are in a task and have the ability to log the message and give the user 
                // feedback as to its cause, tasks if at all possible should not have exception leave them.
                Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.ProblemGeneratingReferencePaths", TargetFrameworkMoniker, e.Message);

                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                _tfmPathsNoProfile = null;
                TargetFrameworkMonikerDisplayName = null;
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Generate the set of chained reference assembly paths
        /// </summary>
        private IList<String> GetPaths(string rootPath, string targetFrameworkFallbackSearchPaths, FrameworkNameVersioning frameworkmoniker)
        {
            IList<String> pathsToReturn = ToolLocationHelper.GetPathToReferenceAssemblies(
                                                frameworkmoniker.Identifier,
                                                frameworkmoniker.Version.ToString(),
                                                frameworkmoniker.Profile,
                                                rootPath,
                                                targetFrameworkFallbackSearchPaths);

            if (!SuppressNotFoundError)
            {
                // No reference assembly paths could be found, log an error so an invalid build will not be produced.
                // 1/26/16: Note this was changed from a warning to an error (see GitHub #173).
                if (pathsToReturn.Count == 0)
                {
                    Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound", frameworkmoniker.ToString());
                }
            }

            return pathsToReturn;
        }

        #endregion
    }
}
