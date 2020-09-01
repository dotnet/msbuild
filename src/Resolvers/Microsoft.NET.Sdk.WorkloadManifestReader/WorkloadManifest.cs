using System.Collections.Generic;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    /// <summary>
    /// An SDK workload manifest
    /// </summary>
    class WorkloadManifest
    {
        public WorkloadManifest(long version, string? description, Dictionary<string, WorkloadDefinition> workloads, Dictionary<string, WorkloadPack> packs)
        {
            Version = version;
            Description = description;
            Workloads = workloads;
            Packs = packs;
        }

        public long Version { get; }
        public string? Description { get; }
        public Dictionary<string, WorkloadDefinition> Workloads { get; }
        public Dictionary<string, WorkloadPack> Packs { get; }
    }
}
