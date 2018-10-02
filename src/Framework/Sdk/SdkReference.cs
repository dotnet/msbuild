// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Represents a software development kit (SDK) that is referenced in a &lt;Project /&gt; or &lt;Import /&gt; element.
    /// </summary>
    public sealed class SdkReference : IEquatable<SdkReference>
    {
        /// <summary>
        ///     Initializes a new instance of the SdkReference class.
        /// </summary>
        /// <param name="name">The name of the SDK.</param>
        /// <param name="version">The version of the SDK.</param>
        /// <param name="minimumVersion">Minimum SDK version required by the project.</param>
        public SdkReference(string name, string version, string minimumVersion)
        {
            Name = name;
            Version = version;
            MinimumVersion = minimumVersion;
        }

        /// <summary>
        ///     Gets the name of the SDK.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the version of the SDK.
        /// </summary>
        public string Version { get; }

        /// <summary>
        ///     Gets the minimum version required. This value is specified by the project to indicate the minimum version of the
        ///     SDK that is required in order to build. This is useful in order to produce an error message if a name match can
        ///     be found but no acceptable version could be resolved.
        /// </summary>
        public string MinimumVersion { get; }

        /// <summary>
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(SdkReference other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) && string.Equals(Version, other.Version) &&
                   string.Equals(MinimumVersion, other.MinimumVersion);
        }

        /// <summary>
        ///     Attempts to parse the specified string as a <see cref="SdkReference" />.  The expected format is:
        ///         SDK, SDK/Version, or SDK/min=MinimumVersion
        ///     Values are not required to specify a version or MinimumVersion.
        /// </summary>
        /// <param name="sdk">An SDK name and version to parse in the format "SDK/Version,min=MinimumVersion".</param>
        /// <param name="sdkReference">A parsed <see cref="SdkReference" /> if the specified value is a valid SDK name.</param>
        /// <returns><code>true</code> if the SDK name was successfully parsed, otherwise <code>false</code>.</returns>
        public static bool TryParse(string sdk, out SdkReference sdkReference)
        {
            sdkReference = null;
            if (string.IsNullOrWhiteSpace(sdk)) return false;

            var parts = sdk.Split('/').Select(i => i.Trim()).ToArray();

            if (parts.Length < 1 || parts.Length > 2) return false;
            if (string.IsNullOrWhiteSpace(parts[0])) return false;

            if (parts.Length == 1 || string.IsNullOrWhiteSpace(parts[1]))
            {
                sdkReference = new SdkReference(parts[0], null, null);
            }
            else if (parts.Length == 2)
            {
                // If the version specified starts with "min=" treat the string as a minimum version, otherwise
                // treat it as a version.
                sdkReference = parts[1].StartsWith("min=", StringComparison.OrdinalIgnoreCase)
                    ? new SdkReference(parts[0], null, parts[1].Substring(4))
                    : new SdkReference(parts[0], parts[1], null);
            }

            return sdkReference != null;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is SdkReference && Equals((SdkReference) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (MinimumVersion != null ? MinimumVersion.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Version) && string.IsNullOrWhiteSpace(MinimumVersion))
            {
                return Name;
            }

            return string.IsNullOrWhiteSpace(Version) ?
                $"{Name}/min={MinimumVersion}" :
                $"{Name}/{Version}";
        }
    }
}
