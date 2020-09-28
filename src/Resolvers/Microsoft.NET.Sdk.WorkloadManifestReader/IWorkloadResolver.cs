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
        string? TryGetPackVersion(string packId);
    }
}
