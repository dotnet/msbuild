// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : IPackWorkloadInstaller
    {
        public IList<PackInfo> InstalledPacks = new List<PackInfo>();
        public IList<PackInfo> RolledBackPacks = new List<PackInfo>();
        public IList<string> WorkloadInstallRecord = new List<string>();
        public bool GarbageCollectionCalled = false;
        private readonly string FailingWorkload;

        public MockPackWorkloadInstaller(string failingWorkload = null)
        {
            FailingWorkload = failingWorkload;
        }

        public override void InstallWorkloadPack(PackInfo packInfo, string featureBand, bool useOfflineCache = false)
        {
            InstalledPacks.Add(packInfo);
        }

        public override void RollBackWorkloadPackInstall(PackInfo packInfo, string featureBand)
        {
            RolledBackPacks.Add(packInfo);
        }

        public override void WriteWorkloadInstallationRecord(string workloadId, string featureBand)
        {
            WorkloadInstallRecord.Add(workloadId);
            if (workloadId.Equals(FailingWorkload))
            {
                throw new Exception($"Failing workload: {workloadId}");
            }
        }

        public override void DeleteWorkloadInstallationRecord(string workloadId, string featureBand)
        {
            WorkloadInstallRecord.Remove(workloadId);
        }

        public override void GarbageCollectInstalledWorkloadPacks() => throw new System.NotImplementedException();
        public override void DownloadToOfflineCache(IReadOnlyCollection<string> manifests) => throw new System.NotImplementedException();
        public override IReadOnlyCollection<string> GetFeatureBandsWithInstallationRecords() => throw new System.NotImplementedException();
        public override IReadOnlyCollection<string> GetInstalledWorkloads(string featureBand) => throw new System.NotImplementedException();
        public override void InstallWorkloadManifest(string manifestId, string manifestVersion, string sdkFeatureBand) => throw new System.NotImplementedException();
    }
}
