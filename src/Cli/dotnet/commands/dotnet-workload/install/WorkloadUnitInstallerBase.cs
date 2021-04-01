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
        public abstract void InstallWorkload(string workloadId, bool useOfflineCache = false);

        public abstract void DownloadToOfflineCache(IReadOnlyCollection<string> manifests);

        public abstract void UninstallWorkload(string workloadId);

        public abstract IReadOnlyCollection<string> ListInstalledWorkloads();

        // Common workload installer methods:
        public abstract void DeleteWorkloadInstallationRecord(string workloadId, string featureBand);
        
        public abstract IReadOnlyCollection<string> GetFeatureBandsWithInstallationRecords();

        public abstract IReadOnlyCollection<string> GetInstalledWorkloads(string featureBand);

        public abstract void InstallWorkloadManifest(string manifestId, string manifestVersion, string sdkFeatureBand);

        public abstract void WriteWorkloadInstallationRecord(string workloadId, string featureBand);
    }
}
