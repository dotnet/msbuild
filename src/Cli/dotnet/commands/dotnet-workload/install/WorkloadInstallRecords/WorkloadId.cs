// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

#nullable disable
namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal class WorkloadId : IEquatable<WorkloadId>, IComparable<WorkloadId>
    {
        private string _id;

        public WorkloadId(string id)
        {
            _id = id?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(id));
        }

        public bool Equals(WorkloadId other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(WorkloadId other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WorkloadId id && Equals(id);
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
