using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    internal class WorkloadDefinition
    {
        public WorkloadDefinition(
            string id, bool isAbstract, string? description, WorkloadDefinitionKind kind, List<string>? extends,
            List<string>? packs, List<string>? platforms)
        {
            Id = id;
            IsAbstract = isAbstract;
            Description = description;
            Kind = kind;
            Extends = extends;
            Packs = packs;
            Platforms = platforms;
        }

        public string Id { get; }
        public bool IsAbstract { get; }
        public string? Description { get; }
        public WorkloadDefinitionKind Kind { get; }
        public List<string>? Extends { get; }
        public List<string>? Packs { get; }
        public List<string>? Platforms { get; }
    }

    internal enum WorkloadDefinitionKind
    {
        Dev,
        Build
    }
}
