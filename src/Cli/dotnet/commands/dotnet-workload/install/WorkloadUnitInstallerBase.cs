// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal abstract class WorkloadUnitInstallerBase : IWorkloadInstaller
    {
        public InstallationUnit GetInstallationUnit()
        {
            return InstallationUnit.Workload;
        }

        // Workload unit unique methods:
        public abstract void InstallWorkload(WorkloadId workloadId, bool useOfflineCache = false);

        public abstract void DownloadToOfflineCache(IReadOnlyCollection<string> manifests);

        public abstract void UninstallWorkload(WorkloadId workloadId);

        public abstract IReadOnlyCollection<string> ListInstalledWorkloads();

        // Common workload installer methods:
        public abstract void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);
        
        public abstract IReadOnlyCollection<SdkFeatureBand> GetFeatureBandsWithInstallationRecords();

        public abstract IReadOnlyCollection<string> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand);

        public abstract void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand);

        public abstract void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);
    }
}
