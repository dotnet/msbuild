// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadPack
    {
        public WorkloadPack(WorkloadPackId id, string version, WorkloadPackKind kind, Dictionary<string, WorkloadPackId>? aliasTo)
        {
            Id = id;
            Version = version;
            Kind = kind;
            AliasTo = aliasTo;
        }

        public WorkloadPackId Id { get; }
        public string Version { get; }
        public WorkloadPackKind Kind { get; }
        public bool IsAlias => AliasTo != null && AliasTo.Count > 0;
        public Dictionary<string, WorkloadPackId>? AliasTo { get; }

        public WorkloadPackId? TryGetAliasForRuntimeIdentifiers(IEnumerable<string> runtimeIdentifiers)
        {
            if (AliasTo == null || AliasTo.Count == 0)
            {
                return null;
            }

            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                if (AliasTo.TryGetValue(runtimeIdentifier, out WorkloadPackId alias))
                {
                    return alias;
                }
            }

            // no alias exists for this platform
            return null;
        }
    }
}
