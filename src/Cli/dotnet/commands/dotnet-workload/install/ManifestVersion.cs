// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal struct ManifestVersion : IEquatable<ManifestVersion>, IComparable<ManifestVersion>
    {
        private string _version;

        public ManifestVersion(string version)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public bool Equals(ManifestVersion other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(ManifestVersion other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ManifestVersion version && Equals(version);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return _version;
        }
    }
}
