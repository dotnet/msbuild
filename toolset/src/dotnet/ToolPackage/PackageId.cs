// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.InternalAbstractions;

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
