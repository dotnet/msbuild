// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.MSBuildSdkResolver;
using Strings = Microsoft.NET.Sdk.Localization.Strings;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class ManifestVersion : IEquatable<ManifestVersion>, IComparable<ManifestVersion>
    {
        private FXVersion _version;

        public ManifestVersion(string version)
        {
            if (!FXVersion.TryParse(version, out _version))
            {
                throw new ArgumentException(Strings.InvalidManifestVersion, version);     
            }
        }

        public bool Equals(ManifestVersion? other)
        {
            return ToString().Equals(other?.ToString());
        }

        public int CompareTo(ManifestVersion? other)
        {
            return FXVersion.Compare(_version, other?._version);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestVersion version && Equals(version);
        }

        public override int GetHashCode()
        {
            return _version.GetHashCode();
        }

        public override string ToString()
        {
            return _version.ToString();
        }
    }
}
