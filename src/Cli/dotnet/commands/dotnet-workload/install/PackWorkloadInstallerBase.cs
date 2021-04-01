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
        public abstract void InstallWorkloadPack(PackInfo packInfo, string featureBand, bool useOfflineCache = false);

        public abstract void RollBackWorkloadPackInstall(PackInfo packInfo, string featureBand);

        public abstract void DownloadToOfflineCache(IReadOnlyCollection<string> manifests);

        public abstract void GarbageCollectInstalledWorkloadPacks();

        // Common workload installer methods:
        public abstract void DeleteWorkloadInstallationRecord(string workloadId, string featureBand);
        
        public abstract IReadOnlyCollection<string> GetFeatureBandsWithInstallationRecords();

        public abstract IReadOnlyCollection<string> GetInstalledWorkloads(string featureBand);

       public abstract void InstallWorkloadManifest(string manifestId, string manifestVersion, string sdkFeatureBand);

        public abstract void WriteWorkloadInstallationRecord(string workloadId, string featureBand);
    }
}
