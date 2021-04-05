// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : PackWorkloadInstallerBase
    {
        public IList<PackInfo> InstalledPacks = new List<PackInfo>();
        public IList<PackInfo> RolledBackPacks = new List<PackInfo>();
        public IList<WorkloadId> WorkloadInstallRecord = new List<WorkloadId>();
        public bool GarbageCollectionCalled = false;
        private readonly string FailingWorkload;

        public MockPackWorkloadInstaller(string failingWorkload = null)
        {
            FailingWorkload = failingWorkload;
        }

        public override void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false)
        {
            InstalledPacks.Add(packInfo);
        }

        public override void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            RolledBackPacks.Add(packInfo);
        }

        public override void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Add(workloadId);
            if (workloadId.Equals(FailingWorkload))
            {
                throw new Exception($"Failing workload: {workloadId}");
            }
        }

        public override void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Remove(workloadId);
        }

        public override void GarbageCollectInstalledWorkloadPacks()
        {
            GarbageCollectionCalled = true;
        }

        public override void DownloadToOfflineCache(IReadOnlyCollection<string> manifests) => throw new System.NotImplementedException();
        public override IReadOnlyCollection<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() => throw new System.NotImplementedException();
        public override IReadOnlyCollection<string> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
        public override void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
    }
}
