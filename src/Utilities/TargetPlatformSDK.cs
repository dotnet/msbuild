// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Internal representation of a target platform</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Structure to represent a target platform sdk
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Dev11 Beta (go-live) is shipping this way")]
    public class TargetPlatformSDK : IEquatable<TargetPlatformSDK>
    {
        /// <summary>
        /// Path to the platform sdk may be null if not a platform sdk.
        /// </summary>
        private string _path;

        /// <summary>
        /// Object containing the properties in the SDK manifest
        /// </summary>
        private SDKManifest _manifest;

        /// <summary>
        /// Cache for min Visual Studio version from manifest
        /// </summary>
        private Version _minVSVersion;

        /// <summary>
        /// Cache for min OS version from manifest
        /// </summary>
        private Version _minOSVersion;

        /// <summary>
        /// Constructor
        /// </summary>
        public TargetPlatformSDK(string targetPlatformIdentifier, Version targetPlatformVersion, string path)
        {
            ErrorUtilities.VerifyThrowArgumentNull(targetPlatformIdentifier, "targetPlatformIdentifier");
            ErrorUtilities.VerifyThrowArgumentNull(targetPlatformVersion, "targetPlatformVersion");
            TargetPlatformIdentifier = targetPlatformIdentifier;
            TargetPlatformVersion = targetPlatformVersion;
            Path = path;
            ExtensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Platforms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Min Visual Studio version from manifest
        /// </summary>
        public Version MinVSVersion
        {
            get
            {
                if (_minVSVersion == null && Manifest != null && Manifest.MinVSVersion != null)
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
        /// Min OS version from manifest
        /// </summary>
        public Version MinOSVersion
        {
            get
            {
                if (_minOSVersion == null && Manifest != null && Manifest.MinOSVersion != null)
                {
                    if (!Version.TryParse(Manifest.MinOSVersion, out _minOSVersion))
                    {
                        _minOSVersion = null;
                    }
                }

                return _minOSVersion;
            }
        }

        /// <summary>
        /// Target platform identifier
        /// </summary>
        public string TargetPlatformIdentifier
        {
            get;
            private set;
        }

        /// <summary>
        /// Target platform version
        /// </summary>
        public Version TargetPlatformVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Path to target platform sdk if it exists, it may not if there is no target platform is installed
        /// </summary>
        public string Path
        {
            get
            {
                return _path;
            }

            set
            {
                if (value != null)
                {
                    _path = FileUtilities.EnsureTrailingSlash(value);
                }
                else
                {
                    _path = null;
                }
            }
        }

        /// <summary>
        /// The SDK's display name, or null if one is not defined. 
        /// </summary>
        public string DisplayName
        {
            get
            {
                return Manifest != null ? Manifest.DisplayName : null;
            }
        }

        /// <summary>
        /// Extension sdks within this platform, 
        /// </summary>
        internal Dictionary<string, string> ExtensionSDKs
        {
            get;
            private set;
        }

        /// <summary>
        /// Set of platforms supported by this SDK. 
        /// </summary>
        internal Dictionary<string, string> Platforms
        {
            get;
            private set;
        }

        /// <summary>
        /// Reference to manifest object
        /// Makes it is instantiated only once 
        /// </summary>
        private SDKManifest Manifest
        {
            get
            {
                // Load manifest from disk the first time it is needed
                if (_manifest == null && _path != null)
                {
                    _manifest = new SDKManifest(_path);
                }

                return _manifest;
            }
        }

        /// <summary>
        /// Override GetHashCode
        /// </summary>
        public override int GetHashCode()
        {
            return TargetPlatformIdentifier.ToLowerInvariant().GetHashCode() ^ TargetPlatformVersion.GetHashCode();
        }

        /// <summary>
        /// Override equals
        /// </summary>
        public override bool Equals(object obj)
        {
            TargetPlatformSDK moniker = obj as TargetPlatformSDK;
            if (moniker == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, moniker))
            {
                return true;
            }

            return Equals(moniker);
        }

        /// <summary>
        /// Implement IEquatable
        /// </summary>
        public bool Equals(TargetPlatformSDK other)
        {
            if (other == null)
            {
                return false;
            }

            return TargetPlatformIdentifier.Equals(other.TargetPlatformIdentifier, StringComparison.OrdinalIgnoreCase) && TargetPlatformVersion.Equals(other.TargetPlatformVersion);
        }

        /// <summary>
        /// Returns true if this SDK supports the given platform, or false otherwise. 
        /// </summary>
        public bool ContainsPlatform(string targetPlatformIdentifier, string targetPlatformVersion)
        {
            string sdkKey = GetSdkKey(targetPlatformIdentifier, targetPlatformVersion);
            return Platforms.ContainsKey(sdkKey);
        }

        /// <summary>
        /// Given an identifier and version, construct a string to use as a key for that combination. 
        /// </summary>
        internal static string GetSdkKey(string sdkIdentifier, string sdkVersion)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}, Version={1}", sdkIdentifier, sdkVersion);
        }
    }
}