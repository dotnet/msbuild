// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal class NetSdkManagedInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        private readonly string _workloadMetadataDir;
        private readonly string _installedWorkloadDir = "InstalledWorkloads";

        public NetSdkManagedInstallationRecordRepository(string dotnetDir)
        {
            _workloadMetadataDir = Path.Combine(dotnetDir, "metadata", "workloads");
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            if (Directory.Exists(_workloadMetadataDir))
            {
                var bands = Directory.EnumerateDirectories(_workloadMetadataDir);
                return bands
                    .Where(band => Directory.Exists(Path.Combine(band, _installedWorkloadDir)) && Directory.GetFiles(Path.Combine(band, _installedWorkloadDir)).Any())
                    .Select(path => new SdkFeatureBand(Path.GetFileName(path)));
            }
            else
            {
                return new List<SdkFeatureBand>();
            }
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir);
            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path)
                    .Select(file => new WorkloadId(Path.GetFileName(file)));
            }
            else
            {
                return new List<WorkloadId>();
            }
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir, workloadId.ToString());
            if (!File.Exists(path))
            {
                var pathDir = Path.GetDirectoryName(path);
                if (pathDir != null && !Directory.Exists(pathDir))
                {
                    Directory.CreateDirectory(pathDir);
                }
                File.Create(path);
            }
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir, workloadId.ToString());
            if (File.Exists(path))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(path);
            }
        }
    }
}
