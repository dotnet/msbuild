// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public abstract class BaseWorkloadDefinition
    {
        public BaseWorkloadDefinition (WorkloadId id)
        {
            Id = id;
        }

        public WorkloadId Id { get; }
    }

    public class WorkloadDefinition : BaseWorkloadDefinition
    {
        public WorkloadDefinition(
            WorkloadId id, bool isAbstract, string? description, WorkloadDefinitionKind kind, List<WorkloadId>? extends,
            List<WorkloadPackId>? packs, List<string>? platforms
            ) : base (id)
        {
            IsAbstract = isAbstract;
            Description = description;
            Kind = kind;
            Extends = extends;
            Packs = packs;
            Platforms = platforms;
        }

        public bool IsAbstract { get; }
        public string? Description { get; }
        public WorkloadDefinitionKind Kind { get; }
        public List<WorkloadId>? Extends { get; }
        public List<WorkloadPackId>? Packs { get; }
        public List<string>? Platforms { get; }
    }

    public enum WorkloadDefinitionKind
    {
        Dev,
        Build
    }

    public class WorkloadRedirect : BaseWorkloadDefinition
    {
        public WorkloadRedirect(WorkloadId id, WorkloadId replaceWith) : base (id)
        {
            ReplaceWith = replaceWith;
        }

        public WorkloadId ReplaceWith { get; }
    }
}
