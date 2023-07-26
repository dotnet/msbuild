// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ToolPackage
{
    internal struct PackageId : IEquatable<PackageId>, IComparable<PackageId>
    {
        private string _id;

        public PackageId(string id)
        {
            _id = id?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(id));
        }

        public bool Equals(PackageId other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(PackageId other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PackageId id && Equals(id);
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
