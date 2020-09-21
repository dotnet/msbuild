// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// An SDK workload manifest
    /// </summary>
    internal class WorkloadManifest
    {
        public WorkloadManifest(long version, string? description, Dictionary<WorkloadDefinitionId, WorkloadDefinition> workloads, Dictionary<WorkloadPackId, WorkloadPack> packs)
        {
            Version = version;
            Description = description;
            Workloads = workloads;
            Packs = packs;
        }

        public long Version { get; }
        public string? Description { get; }
        public Dictionary<WorkloadDefinitionId, WorkloadDefinition> Workloads { get; }
        public Dictionary<WorkloadPackId, WorkloadPack> Packs { get; }
    }
}
