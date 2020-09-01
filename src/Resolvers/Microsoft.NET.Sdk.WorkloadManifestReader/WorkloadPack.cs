using System.Collections.Generic;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    class WorkloadPack
    {
        public WorkloadPack(string id, string version, WorkloadPackKind kind, Dictionary<string, string>? aliasTo)
        {
            Id = id;
            Version = version;
            Kind = kind;
            AliasTo = aliasTo;
        }

        public string Id { get; }
        public string Version { get; }
        public WorkloadPackKind Kind { get; }
        public bool IsAlias => AliasTo != null && AliasTo.Count > 0;
        public Dictionary<string, string>? AliasTo { get; }
    }
}
