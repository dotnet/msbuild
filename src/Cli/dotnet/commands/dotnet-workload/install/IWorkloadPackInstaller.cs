// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadPackInstaller : IInstaller
    {
        void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false);

        void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand);

        void DownloadToOfflineCache(IEnumerable<string> manifests);

        void GarbageCollectInstalledWorkloadPacks();
    }
}
