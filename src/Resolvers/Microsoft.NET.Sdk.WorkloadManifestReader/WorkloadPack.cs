// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    internal class WorkloadPack
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

        public WorkloadPackId? TryGetAliasForPlatformIds (IEnumerable<string> platformIds)
        {
            if (AliasTo == null || AliasTo.Count == 0)
            {
                return null;
            }

            foreach (var platformId in platformIds)
            {
                if (AliasTo.TryGetValue(platformId, out WorkloadPackId alias))
                {
                    return alias;
                }
            }

            // no alias exists for this platform
            return null;
        }
    }
}
