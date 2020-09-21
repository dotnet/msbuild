// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// Wraps a workload definition id string to help ensure consistency of behaviour/semantics.
    /// Comparisons are case insensitive but ToString() will return the original string for display purposes.
    /// </summary>
    internal readonly struct WorkloadDefinitionId : IComparable<WorkloadDefinitionId>, IEquatable<WorkloadDefinitionId>
    {
        private readonly string _id;

        public WorkloadDefinitionId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace", nameof(id));
            }

            _id = id;
        }

        public int CompareTo(WorkloadDefinitionId other) => string.Compare(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public bool Equals(WorkloadDefinitionId other) => string.Equals(_id, other._id, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_id);

        public override bool Equals(object? obj) => obj is WorkloadDefinitionId id && Equals(id);

        public override string ToString() => _id;
    }
}
