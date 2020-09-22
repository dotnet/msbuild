// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Workload pack ids are NuGet ids: comparisons are case insensitive, and nupkg files are expected to be stored with "canonical" lowercased names.
    /// However, display strings and paths in the sdk/packs/* folder use original casing.
    /// This internal struct helps preserve/annotate these semantics.
    /// </summary>
    /// <remarks>We also use this for workload definition ids for consistency</remarks>
    internal struct WorkloadPackId : IComparable<WorkloadPackId>, IEquatable<WorkloadPackId>
    {
        string _id;

        public WorkloadPackId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace", nameof(id));
            }

            _id = id;
        }

        public int CompareTo(WorkloadPackId other) => string.Compare(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public bool Equals(WorkloadPackId other) => string.Equals(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_id);

        public override bool Equals(object? obj) => obj is WorkloadPackId id && Equals(id);

        public override string ToString() => _id;

        public string GetNuGetCanonicalId() => _id.ToLowerInvariant();
    }
}
