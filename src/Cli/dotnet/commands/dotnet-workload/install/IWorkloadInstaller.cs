// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadInstaller : IInstaller
    {
        void InstallWorkload(WorkloadId workloadId, DirectoryPath? offlineCache = null);

        void DownloadToOfflineCache(WorkloadId workload, DirectoryPath offlineCache, bool includePreviews);

        void UninstallWorkload(WorkloadId workloadId);
    }
}
