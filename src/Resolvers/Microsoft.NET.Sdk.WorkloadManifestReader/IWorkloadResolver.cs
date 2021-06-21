// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public interface IWorkloadResolver
    {
        IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind);
        IEnumerable<string> GetPacksInWorkload(string workloadId);
        ISet<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<string> packId);
        IEnumerable<WorkloadDefinition> GetAvaliableWorkloads();
        WorkloadResolver CreateTempDirResolver(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion);
        bool IsWorkloadPlatformCompatible(WorkloadId workloadId);
        string GetManifestVersion(string manifestId);

        /// <summary>
        /// Resolve the pack for this resolver's SDK band.
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild SDK resolver to look up which versions of the SDK packs to import.
        /// NOTE: The pack path may use an aliased ID.
        /// </remarks>
        /// <param name="packId">A workload pack ID</param>
        /// <returns>Information about the workload pack, or null if the specified pack ID isn't found in the manifests</returns>
        WorkloadResolver.PackInfo? TryGetPackInfo(string packId);

        /// <summary>
        /// Refresh workload and pack information based on the current installed workload manifest files
        /// </summary>
        void RefreshWorkloadManifests();
    }
}
