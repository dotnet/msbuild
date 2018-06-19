// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Resolve reference which have SDKName metadata on them</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    ///There is no search path element because the only way to get this resolver is by having the SDKName metadata on the reference.
    /// </summary>
    internal class InstalledSDKResolver : Resolver
    {
        /// <summary>
        ///  Resolved SDKs
        /// </summary>
        private readonly Dictionary<string, ITaskItem> _resolvedSDKs;

        /// <summary>
        /// Construct.
        /// </summary>
        public InstalledSDKResolver(Dictionary<string, ITaskItem> resolvedSDKs, string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, System.Reflection.ProcessorArchitecture.None, false)
        {
            _resolvedSDKs = resolvedSDKs;
        }

        /// <summary>
        /// Resolve references which are found in a specific SDK
        /// </summary>
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
                // We have found a resolved SDK item that matches the one on the reference items.
                if (_resolvedSDKs.ContainsKey(sdkName))
                {
                    ITaskItem resolvedSDK = _resolvedSDKs[sdkName];

                    string sdkDirectory = resolvedSDK.ItemSpec;
                    string configuration = resolvedSDK.GetMetadata("TargetedSDKConfiguration");
                    string architecture = resolvedSDK.GetMetadata("TargetedSDKArchitecture");

                    string referenceAssemblyFilePath = Path.Combine(sdkDirectory, "References", configuration, architecture);
                    string referenceAssemblyCommonArchFilePath = Path.Combine(sdkDirectory, "References", "CommonConfiguration", architecture);
                    string referenceAssemblyPathNeutral = Path.Combine(sdkDirectory, "References", configuration, "Neutral");
                    string referenceAssemblyArchFilePathNeutral = Path.Combine(sdkDirectory, "References", "CommonConfiguration", "Neutral");

                    string[] searchLocations = new string[]
                    {
                        referenceAssemblyFilePath, // Config-Arch
                        referenceAssemblyPathNeutral, // Config-Neutral
                        referenceAssemblyCommonArchFilePath, // CommonArch-Config
                        referenceAssemblyArchFilePathNeutral // CommonArch-Neutral
                    };

                    // Lets try and resovle from the windowsmetadata directory first

                    // Go through the search locations and find the assembly
                    foreach (string searchLocation in searchLocations)
                    {
                        string resolvedPath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, searchLocation, assembliesConsideredAndRejected);

                        if (resolvedPath != null)
                        {
                            foundPath = resolvedPath;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
