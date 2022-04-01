// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : IWorkloadPackInstaller
    {
        public IList<PackInfo> InstalledPacks;
        public List<PackInfo> RolledBackPacks = new List<PackInfo>();
        public IList<(ManifestVersionUpdate manifestUpdate, DirectoryPath? offlineCache)> InstalledManifests = 
            new List<(ManifestVersionUpdate manifestUpdate, DirectoryPath?)>();
        public IList<PackInfo> CachedPacks = new List<PackInfo>();
        public string CachePath;
        public bool GarbageCollectionCalled = false;
        public MockInstallationRecordRepository InstallationRecordRepository;
        public bool FailingRollback;
        public bool FailingGarbageCollection;
        private readonly string FailingPack;

        public int ExitCode => 0;

        public MockPackWorkloadInstaller(string failingWorkload = null, string failingPack = null, bool failingRollback = false, IList<WorkloadId> installedWorkloads = null, 
            IList<PackInfo> installedPacks = null, bool failingGarbageCollection = false)
        {
            InstallationRecordRepository = new MockInstallationRecordRepository(failingWorkload, installedWorkloads);
            FailingRollback = failingRollback;
            InstalledPacks = installedPacks ?? new List<PackInfo>();
            FailingPack = failingPack;
            FailingGarbageCollection = failingGarbageCollection;
        }

        public void InstallWorkloadPacks(IEnumerable<PackInfo> packInfos, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            transactionContext.Run(action: () =>
            {
                foreach (var packInfo in packInfos)
                {
                    InstalledPacks = InstalledPacks.Append(packInfo).ToList();
                    CachePath = offlineCache?.Value;
                    if (packInfo.Id.ToString().Equals(FailingPack))
                    {
                        throw new Exception($"Failing pack: {packInfo.Id}");
                    }
                }
            },
            rollback: () =>
            {
                if (FailingRollback)
                {
                    throw new Exception("Rollback failure");
                }

                RolledBackPacks.AddRange(packInfos);
            });
        }

        public void RepairWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, ITransactionContext context, DirectoryPath? offlineCache = null)
        {
            InstallWorkloadPacks(new[] { packInfo }, sdkFeatureBand, context, offlineCache);
        }

        public void GarbageCollectInstalledWorkloadPacks(DirectoryPath? offlineCache = null)
        {
            if (FailingGarbageCollection)
            {
                throw new Exception("Failing garbage collection");
            }
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

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null, bool isRollback = false)
        {
            InstalledManifests.Add((manifestUpdate, offlineCache));
        }

        public void DownloadToOfflineCache(PackInfo pack, DirectoryPath cachePath, bool includePreviews)
        {
            CachedPacks.Add(pack);
            CachePath = cachePath.Value;
        }

        public IEnumerable<(WorkloadPackId, string)> GetInstalledPacks(SdkFeatureBand sdkFeatureBand)
        {
            return InstalledPacks.Select(pack => (pack.Id, pack.Version));
        }

        public IWorkloadInstaller GetWorkloadInstaller() => throw new NotImplementedException();

        public void Shutdown()
        {

        }
    }

    internal class MockInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        public IList<WorkloadId> WorkloadInstallRecord = new List<WorkloadId>();
        private readonly string FailingWorkload;
        public IList<WorkloadId> InstalledWorkloads;

        public MockInstallationRecordRepository(string failingWorkload = null, IList<WorkloadId> installedWorkloads = null)
        {
            FailingWorkload = failingWorkload;
            InstalledWorkloads = installedWorkloads ?? new List<WorkloadId>();
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
            return InstalledWorkloads;
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            return Enumerable.Empty<SdkFeatureBand>();
        }
    }
}
