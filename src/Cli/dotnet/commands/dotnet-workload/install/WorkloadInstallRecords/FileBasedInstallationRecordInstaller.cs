// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal class FileBasedInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        private readonly string _workloadMetadataDir;
        private const string InstalledWorkloadDir = "InstalledWorkloads";

        public FileBasedInstallationRecordRepository(string workloadMetadataDir)
        {
            _workloadMetadataDir = workloadMetadataDir;
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            if (Directory.Exists(_workloadMetadataDir))
            {
                var bands = Directory.EnumerateDirectories(_workloadMetadataDir);
                return bands
                    .Where(band => Directory.Exists(Path.Combine(band, InstalledWorkloadDir)) && Directory.GetFiles(Path.Combine(band, InstalledWorkloadDir)).Any())
                    .Select(path => new SdkFeatureBand(Path.GetFileName(path)));
            }
            else
            {
                return new List<SdkFeatureBand>();
            }
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), InstalledWorkloadDir);
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
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), InstalledWorkloadDir, workloadId.ToString());
            if (!File.Exists(path))
            {
                var pathDir = Path.GetDirectoryName(path);
                if (pathDir != null && !Directory.Exists(pathDir))
                {
                    Directory.CreateDirectory(pathDir);
                }
                File.Create(path).Close();
            }
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), InstalledWorkloadDir, workloadId.ToString());
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
