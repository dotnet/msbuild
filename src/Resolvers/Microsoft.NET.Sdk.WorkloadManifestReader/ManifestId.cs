// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public struct ManifestId : IEquatable<ManifestId>, IComparable<ManifestId>
    {
        private string _id;

        public ManifestId(string id)
        {
            _id = id?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(id));
        }

        public bool Equals(ManifestId other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(ManifestId other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return _id ?? "";
        }
    }
}
