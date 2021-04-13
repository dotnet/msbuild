// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class ManifestVersion : IEquatable<ManifestVersion>, IComparable<ManifestVersion>
    {
        private long _version;

        public ManifestVersion(string version)
        {
            _version = long.Parse(version);
        }

        public ManifestVersion(long version)
        {
            _version = version;
        }

        public bool Equals(ManifestVersion other)
        {
            return _version == other._version;
        }

        public int CompareTo(ManifestVersion other)
        {
            return _version.CompareTo(other._version);
        }

        public override bool Equals(object obj)
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
