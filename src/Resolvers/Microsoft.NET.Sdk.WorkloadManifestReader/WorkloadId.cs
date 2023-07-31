// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Wraps a workload definition id string to help ensure consistency of behaviour/semantics.
    /// Comparisons are case insensitive but ToString() will return the original string for display purposes.
    /// </summary>
    public readonly struct WorkloadId : IComparable<WorkloadId>, IEquatable<WorkloadId>
    {
        private readonly string _id;

        public WorkloadId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or empty", nameof(id));
            }

            _id = id;
        }

        public int CompareTo(WorkloadId other) => string.Compare(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public bool Equals(WorkloadId other) => string.Equals(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_id);

        public override bool Equals(object? obj) => obj is WorkloadId id && Equals(id);

        public override string ToString() => _id;

        public static implicit operator string(WorkloadId id) => id._id;

        public static bool operator ==(WorkloadId a, WorkloadId b) => a.Equals(b);

        public static bool operator !=(WorkloadId a, WorkloadId b) => !a.Equals(b);
    }
}
