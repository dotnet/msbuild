// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal interface IWorkloadsRepositoryEnumerator
    {
        IEnumerable<WorkloadId> InstalledSdkWorkloadIds { get; }
        InstalledWorkloadsCollection AddInstalledVsWorkloads(IEnumerable<WorkloadId> sdkWorkloadIds);

        /// <summary>
        /// Gets deduplicated enumeration of transitive closure of 'extends' relation of installed workloads.
        /// </summary>
        /// <returns>Deduplicated enumeration of workload infos.</returns>
        IEnumerable<WorkloadResolver.WorkloadInfo> InstalledAndExtendedWorkloads { get; }
    }
}
