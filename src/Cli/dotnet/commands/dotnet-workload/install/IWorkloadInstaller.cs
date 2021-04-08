// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadInstaller : IInstaller
    {
        void InstallWorkload(WorkloadId workloadId, bool useOfflineCache = false);

        void DownloadToOfflineCache(IEnumerable<string> manifests);

        void UninstallWorkload(WorkloadId workloadId);

        IEnumerable<string> ListInstalledWorkloads();
    }
}
