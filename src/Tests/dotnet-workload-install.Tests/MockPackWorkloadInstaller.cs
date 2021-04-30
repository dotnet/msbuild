// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : IWorkloadPackInstaller
    {
        public IList<PackInfo> InstalledPacks = new List<PackInfo>();
        public IList<PackInfo> RolledBackPacks = new List<PackInfo>();
        public IList<(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand)> InstalledManifests = 
            new List<(ManifestId, ManifestVersion, SdkFeatureBand)>();
        public bool GarbageCollectionCalled = false;
        public MockInstallationRecordRepository InstallationRecordRepository;
        public bool FailingRollback;

        public MockPackWorkloadInstaller(string failingWorkload = null, bool failingRollback = false)
        {
            InstallationRecordRepository = new MockInstallationRecordRepository(failingWorkload);
            FailingRollback = failingRollback;
        }

        public void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false)
        {
            InstalledPacks.Add(packInfo);
        }

        public void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            if (FailingRollback)
            {
                throw new Exception("Rollback failure");
            }
            RolledBackPacks.Add(packInfo);
        }

        public void GarbageCollectInstalledWorkloadPacks()
        {
            GarbageCollectionCalled = true;
        }

        public InstallationUnit GetInstallationUnit()
        {
            return InstallationUnit.Packs;
        }

        public IWorkloadPackInstaller GetPackInstaller()
        {
            return this;
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository()
        {
            return InstallationRecordRepository;
        }

        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand)
        {
            InstalledManifests.Add((manifestId, manifestVersion, sdkFeatureBand));
        }

        public void DownloadToOfflineCache(IEnumerable<string> manifests) => throw new System.NotImplementedException();
        public IWorkloadInstaller GetWorkloadInstaller() => throw new NotImplementedException();
    }

    internal class MockInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        public IList<WorkloadId> WorkloadInstallRecord = new List<WorkloadId>();
        private readonly string FailingWorkload;

        public MockInstallationRecordRepository(string failingWorkload = null)
        {
            FailingWorkload = failingWorkload;
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Add(workloadId);
            if (workloadId.ToString().Equals(FailingWorkload))
            {
                throw new Exception($"Failing workload: {workloadId}");
            }
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Remove(workloadId);
        }
        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        {
            return new List<WorkloadId>();
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() => throw new NotImplementedException();
    }
}
