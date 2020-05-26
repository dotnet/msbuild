#nullable enable

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     Storage for the XML attributes locations for the matching <see cref="SdkReference" /> values.
    /// </summary>
    internal sealed class SdkReferenceOrigin : IEquatable<SdkReferenceOrigin>
    {
        public readonly IElementLocation? MinimumVersion;
        public readonly IElementLocation? Name;
        public readonly IElementLocation? Version;

        public SdkReferenceOrigin(IElementLocation? name, IElementLocation? version, IElementLocation? minimumVersion)
        {
            Name = name;
            Version = version;
            MinimumVersion = minimumVersion;
        }

        /// <inheritdoc />
        public bool Equals(SdkReferenceOrigin? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Name, other.Name) && Equals(Version, other.Version) &&
                   Equals(MinimumVersion, other.MinimumVersion);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || obj is SdkReferenceOrigin other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name != null ? Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (MinimumVersion != null ? MinimumVersion.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(SdkReferenceOrigin? left, SdkReferenceOrigin? right) => Equals(left, right);

        public static bool operator !=(SdkReferenceOrigin? left, SdkReferenceOrigin? right) => !Equals(left, right);

        /// <inheritdoc />
        public override string ToString() =>
            $"{nameof(Name)}: {FormatNullable(Name)}, {nameof(Version)}: {FormatNullable(Version)}, {nameof(MinimumVersion)}: {FormatNullable(MinimumVersion)}";

        private static string FormatNullable(IElementLocation? elementLocation) =>
            elementLocation?.ToString() ?? "<null>";
    }
}
