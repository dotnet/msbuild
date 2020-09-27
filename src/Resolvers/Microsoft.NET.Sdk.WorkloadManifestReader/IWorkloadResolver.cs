// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public interface IWorkloadResolver
    {
        IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind);
        IEnumerable<string> GetPacksInWorkload(string workloadId);
        IList<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<string> packId);
        /// <summary>
        /// Gets information about a workload pack from the manifests, whether or not the pack is installed
        /// </summary>
        /// <param name="packId">A workload pack ID</param>
        /// <returns>Information about the workload pack, or null if the specified pack ID isn't found in the manifests</returns>
        WorkloadResolver.PackInfo? TryGetPackInfo(string packId);
    }
}
