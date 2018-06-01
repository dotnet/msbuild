// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
//    Gathers the list of installed SDKS in the registry and on disk and outputs them into the project
//    so they can be used during SDK reference resolution and RAR for single files.
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    ///  Gathers the list of installed SDKS in the registry and on disk and outputs them into the project
    ///  so they can be used during SDK reference resolution and RAR for single files.
    /// </summary>
    public class GetInstalledSDKLocations : TaskExtension
    {
        /// <summary>
        /// Metadata name for directory roots on installed SDK items
        /// </summary>
        internal const string DirectoryRootsMetadataName = "DirectoryRoots";

        /// <summary>
        /// Metadata name for extension directory roots on installed SDK items
        /// </summary>
        internal const string ExtensionDirectoryRootsMetadataName = "ExtensionDirectoryRoots";

        /// <summary>
        /// Metadata name for SDK Name
        /// </summary>
        internal const string SDKNameMetadataName = "SDKName";

        /// <summary>
        /// Metadata name for registry roots on installed SDK items
        /// </summary>
        internal const string RegistryRootMetadataName = "RegistryRoot";

        /// <summary>
        /// Key into our build cache
        /// </summary>
        private const string StaticSDKCacheKey = "StaticToolLocationHelperSDKCacheDisposer";

        #region Properties

        /// <summary>
        /// Target platform version
        /// </summary>
        private string _targetPlatformVersion = String.Empty;

        /// <summary>
        /// Target platform identifier
        /// </summary>
        private string _targetPlatformIdentifier = String.Empty;

        /// <summary>
        /// Platform version we are targeting
        /// </summary>
        [Required]
        public string TargetPlatformVersion
        {
            get => _targetPlatformVersion;

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(TargetPlatformVersion));
                _targetPlatformVersion = value;
            }
        }

        /// <summary>
        /// Platform identifier we are targeting
        /// </summary>
        [Required]
        public string TargetPlatformIdentifier
        {
            get => _targetPlatformIdentifier;

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(TargetPlatformIdentifier));
                _targetPlatformIdentifier = value;
            }
        }

        /// <summary>
        /// Root registry root to look for SDKs
        /// </summary>
        public string SDKRegistryRoot { get; set; }

        /// <summary>
        /// Root directory on disk to look for SDKs
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public string[] SDKDirectoryRoots { get; set; }

        /// <summary>
        /// Root directories on disk to look for new style extension SDKs
        /// </summary>
        public string[] SDKExtensionDirectoryRoots { get; set; }

        /// <summary>
        /// When set to true, the task will produce a warning if there were no SDKs found.
        /// </summary>
        public bool WarnWhenNoSDKsFound { get; set; } = true;

        /// <summary>
        /// Set of items that represent all of the installed SDKs found in the SDKDirectory and SDKRegistry roots.
        /// The itemspec is the SDK install location. There is a piece of metadata called SDKName which contains the name of the SDK.
        /// </summary>
        [Output]
        public ITaskItem[] InstalledSDKs { get; set; }
        #endregion

        #region ITask Members

        /// <summary>
        /// Get the SDK.
        /// </summary>
        public override bool Execute()
        {
            // TargetPlatformVersion and TargetPlatformIdentifier are requried to correctly look for SDKs.
            if (String.IsNullOrEmpty(TargetPlatformVersion) || String.IsNullOrEmpty(TargetPlatformIdentifier))
            {
                Log.LogErrorWithCodeFromResources("GetInstalledSDKs.TargetPlatformInformationMissing");
                return false;
            }

            // Dictionary of ESDKs. Each entry is a (location, platform version) tuple
            IDictionary<string, Tuple<string, string>> installedSDKs = null;

            try
            {
                Log.LogMessageFromResources("GetInstalledSDKs.SearchingForSDKs", _targetPlatformIdentifier, _targetPlatformVersion);

                Version platformVersion = Version.Parse(TargetPlatformVersion);
                installedSDKs = ToolLocationHelper.GetPlatformExtensionSDKLocationsAndVersions(SDKDirectoryRoots, SDKExtensionDirectoryRoots, SDKRegistryRoot, TargetPlatformIdentifier, platformVersion);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("GetInstalledSDKs.CouldNotGetSDKList", e.Message);
            }

            var outputItems = new List<ITaskItem>();

            if (installedSDKs != null && installedSDKs.Count > 0)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "GetInstalledSDKs.FoundSDKs", installedSDKs.Count);
                Log.LogMessageFromResources(MessageImportance.Low, "GetInstalledSDKs.ListInstalledSDKs");

                foreach (KeyValuePair<string, Tuple<string, string>> sdk in installedSDKs)
                {
                    string sdkInfo = ResourceUtilities.FormatResourceString("GetInstalledSDKs.SDKNameAndLocation", sdk.Key, sdk.Value.Item1);
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveAssemblyReference.FourSpaceIndent", sdkInfo);

                    var item = new TaskItem(sdk.Value.Item1);
                    item.SetMetadata("SDKName", sdk.Key);
                    item.SetMetadata("PlatformVersion", sdk.Value.Item2);

                    // Need to stash these so we can unroll the platform via GetMatchingPlatformSDK when we get the reference files for the sdks
                    item.SetMetadata(DirectoryRootsMetadataName, String.Join(";", SDKDirectoryRoots ?? Array.Empty<string>()));
                    item.SetMetadata(ExtensionDirectoryRootsMetadataName, String.Join(";", SDKExtensionDirectoryRoots ?? Array.Empty<string>()));
                    item.SetMetadata(RegistryRootMetadataName, SDKRegistryRoot);

                    outputItems.Add(item);
                }
            }
            else
            {
                if (WarnWhenNoSDKsFound)
                {
                    Log.LogWarningWithCodeFromResources("GetInstalledSDKs.NoSDksFound", TargetPlatformIdentifier, TargetPlatformVersion);
                }
            }

            InstalledSDKs = outputItems.ToArray();

            // We need to register an object so that at the end of the build we will clear the static toolLocationhelper caches.
            // this is important because if someone adds an SDK between builds we would not know about it and not be able to use it.
            // This code is mainly used to deal with the case where msbuild nodes hang around between builds.
            if (BuildEngine is IBuildEngine4 buildEngine4)
            {
                object staticCacheDisposer = buildEngine4.GetRegisteredTaskObject(StaticSDKCacheKey, RegisteredTaskObjectLifetime.Build);
                if (staticCacheDisposer == null)
                {
                    BuildCacheDisposeWrapper staticDisposer = new BuildCacheDisposeWrapper(ToolLocationHelper.ClearSDKStaticCache);
                    buildEngine4.RegisterTaskObject(StaticSDKCacheKey, staticDisposer, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
                }
            }

            return !Log.HasLoggedErrors;
        }

        #endregion
    }
}
