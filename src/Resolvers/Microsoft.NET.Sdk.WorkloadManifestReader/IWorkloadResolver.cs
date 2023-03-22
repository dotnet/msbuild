// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public interface IWorkloadResolver
    {
        IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind);
        IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadId workloadId);
        /// <summary>
        /// Gets deduplicated enumeration of transitive closure of 'extends' relation of given workloads. Given workloads are included as well.
        /// </summary>
        /// <param name="workloadIds">Ids of workloads whose base workloads should be traversed.</param>
        /// <returns>Deduplicated enumeration of workload infos.</returns>
        IEnumerable<WorkloadResolver.WorkloadInfo> GetExtendedWorkloads(IEnumerable<WorkloadId> workloadIds);
        ISet<WorkloadResolver.WorkloadInfo>? GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packId, out ISet<WorkloadPackId> unsatisfiablePacks);
        IEnumerable<WorkloadResolver.WorkloadInfo> GetAvailableWorkloads();
        bool IsPlatformIncompatibleWorkload(WorkloadId workloadId);
        string GetManifestVersion(string manifestId);
        IEnumerable<WorkloadManifestInfo> GetInstalledManifests();
        string GetSdkFeatureBand();
        IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads);
        WorkloadManifest GetManifestFromWorkload(WorkloadId workloadId);

        /// <summary>
        /// Resolve the pack for this resolver's SDK band.
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild SDK resolver to look up which versions of the SDK packs to import.
        /// NOTE: The pack path may use an aliased ID.
        /// </remarks>
        /// <param name="packId">A workload pack ID</param>
        /// <returns>Information about the workload pack, or null if the specified pack ID isn't found in the manifests</returns>
        WorkloadResolver.PackInfo? TryGetPackInfo(WorkloadPackId packId);

        /// <summary>
        /// Refresh workload and pack information based on the current installed workload manifest files
        /// </summary>
        /// <remarks>This is not valid for overlay resolvers</remarks>
        void RefreshWorkloadManifests();

        /// <summary>
        /// Derives a resolver from this resolver by overlaying a set of updated manifests and recomposing.
        /// </summary>
        WorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider);
    }
}
