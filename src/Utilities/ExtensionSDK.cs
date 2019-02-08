// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Structure to represent an extension sdk
    /// </summary>
    internal class ExtensionSDK
    {
        /// <summary>
        /// Path to the platform sdk may be null if not a platform sdk.
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// Extension SDK moniker
        /// </summary>
        private readonly string _sdkMoniker;

        /// <summary>
        /// SDK version
        /// </summary>
        private Version _sdkVersion;

        /// <summary>
        /// SDK identifier
        /// </summary>
        private string _sdkIdentifier;

        /// <summary>
        /// Object containing the properties in the SDK manifest
        /// </summary>
        private SDKManifest _manifest;

        /// <summary>
        /// Caches minimum Visual Studio version from the manifest
        /// </summary>
        private Version _minVSVersion;

        /// <summary>
        /// Caches max platform version from the manifest
        /// </summary>
        private Version _maxPlatformVersion;

        /// <summary>
        /// Constructor
        /// </summary>
        public ExtensionSDK(string extensionSdkMoniker, string extensionSdkPath)
        {
            _sdkMoniker = extensionSdkMoniker;
            _path = extensionSdkPath;
        }

        /// <summary>
        /// SDK version from the moniker
        /// </summary>
        public Version Version
        {
            get
            {
                if (_sdkVersion == null)
                {
                    ParseMoniker(_sdkMoniker);
                }

                return _sdkVersion;
            }
        }

        /// <summary>
        /// SDK identifier from the moniker
        /// </summary>
        public string Identifier
        {
            get
            {
                if (_sdkIdentifier == null)
                {
                    ParseMoniker(_sdkMoniker);
                }

                return _sdkIdentifier;
            }
        }

        /// <summary>
        /// The type of the SDK.
        /// </summary>
        public SDKType SDKType => Manifest.SDKType;

        /// <summary>
        /// Minimum Visual Studio version from SDKManifest.xml
        /// </summary>
        public Version MinVSVersion
        {
            get
            {
                if (_minVSVersion == null && Manifest.MinVSVersion != null)
                {
                    if (!Version.TryParse(Manifest.MinVSVersion, out _minVSVersion))
                    {
                        _minVSVersion = null;
                    }
                }

                return _minVSVersion;
            }
        }

        /// <summary>
        /// Maximum platform version from SDKManifest.xml
        /// </summary>
        public Version MaxPlatformVersion
        {
            get
            {
                if (_maxPlatformVersion == null && Manifest.MaxPlatformVersion != null)
                {
                    if (!Version.TryParse(Manifest.MaxPlatformVersion, out _maxPlatformVersion))
                    {
                        _maxPlatformVersion = null;
                    }
                }

                return _maxPlatformVersion;
            }
        }

        /// <summary>
        /// Api contracts from the SDKManifest, if any
        /// </summary>
        public ICollection<ApiContract> ApiContracts => Manifest.ApiContracts;

        /// <summary>
        /// Reference to the manifest object
        /// Makes sure manifest is instantiated only once
        /// </summary>
        /// <remarks>Load manifest from disk the first time it is needed</remarks>
        private SDKManifest Manifest => _manifest ?? (_manifest = new SDKManifest(_path));

        /// <summary>
        /// Parse SDK moniker
        /// </summary>
        private void ParseMoniker(string moniker)
        {
            string[] properties = moniker.Split(MSBuildConstants.CommaChar);

            foreach (string property in properties)
            {
                string[] words = property.Split(MSBuildConstants.EqualsChar);

                if (words[0].Trim().StartsWith("Version", StringComparison.OrdinalIgnoreCase))
                {
                    if (words.Length > 1 && Version.TryParse(words[1], out Version ver))
                    {
                        _sdkVersion = ver;
                    }
                }
                else
                {
                    _sdkIdentifier = words[0];
                }
            }
        }
    }
}
