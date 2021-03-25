// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadDefinition
    {
        public WorkloadDefinition(
            WorkloadDefinitionId id, bool isAbstract, string? description, WorkloadDefinitionKind kind, List<WorkloadDefinitionId>? extends,
            List<WorkloadPackId>? packs, List<string>? platforms)
        {
            Id = id;
            IsAbstract = isAbstract;
            Description = description;
            Kind = kind;
            Extends = extends;
            Packs = packs;
            Platforms = platforms;
        }

        public WorkloadDefinitionId Id { get; }
        public bool IsAbstract { get; }
        public string? Description { get; }
        public WorkloadDefinitionKind Kind { get; }
        public List<WorkloadDefinitionId>? Extends { get; }
        public List<WorkloadPackId>? Packs { get; }
        public List<string>? Platforms { get; }
    }

    public enum WorkloadDefinitionKind
    {
        Dev,
        Build
    }
}
