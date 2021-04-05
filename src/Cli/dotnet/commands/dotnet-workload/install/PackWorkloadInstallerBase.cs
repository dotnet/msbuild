// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal abstract class PackWorkloadInstallerBase : IWorkloadInstaller
    {
        public InstallationUnit GetInstallationUnit()
        {
            return InstallationUnit.Packs;
        }

        // Workload pack installer unique methods:
        public abstract void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false);

        public abstract void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand);

        public abstract void DownloadToOfflineCache(IReadOnlyCollection<string> manifests);

        public abstract void GarbageCollectInstalledWorkloadPacks();

        // Common workload installer methods:
        public abstract void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);
        
        public abstract IReadOnlyCollection<SdkFeatureBand> GetFeatureBandsWithInstallationRecords();

        public abstract IReadOnlyCollection<string> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand);

       public abstract void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand);

        public abstract void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);
    }
}
