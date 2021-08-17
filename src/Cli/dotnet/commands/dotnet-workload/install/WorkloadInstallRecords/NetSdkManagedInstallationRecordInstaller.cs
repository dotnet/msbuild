// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal class NetSdkManagedInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        private readonly string _dotnetDir;
        private readonly string _dotnetWorkloadMetadataDir;
        private readonly string _userWorkloadMetadataDir;
        private const string InstalledWorkloadDir = "InstalledWorkloads";

        public NetSdkManagedInstallationRecordRepository(string dotnetDir, string userProfileDir)
        {
            _dotnetDir = dotnetDir;
            _dotnetWorkloadMetadataDir = Path.Combine(dotnetDir, "metadata", "workloads");

            userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _userWorkloadMetadataDir = Path.Combine(userProfileDir, "metadata", "workloads");
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            if (Directory.Exists(_dotnetWorkloadMetadataDir))
            {
                var dotnetBands = Directory.EnumerateDirectories(_dotnetWorkloadMetadataDir)
                                    .Where(band => HasInstalledWorkload(band));
                var userBands = Directory.Exists(_userWorkloadMetadataDir)
                                    ? Directory.EnumerateDirectories(_userWorkloadMetadataDir)
                                      .Where(band => WorkloadInstall.IsUserLocal(_dotnetDir, band) && HasInstalledWorkload(band))
                                    : Enumerable.Empty<string>();
                return dotnetBands
                       .Concat(userBands)
                       .Select(path => new SdkFeatureBand(Path.GetFileName(path)));
            }
            else
            {
                return new List<SdkFeatureBand>();
            }

            static bool HasInstalledWorkload(string bandDir)
                => Directory.Exists(Path.Combine(bandDir, InstalledWorkloadDir)) && Directory.GetFiles(Path.Combine(bandDir, InstalledWorkloadDir)).Any();
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand featureBand)
        {
            var path = Path.Combine(GetSdkWorkloadMetadataDir(featureBand), InstalledWorkloadDir);
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
            var path = Path.Combine(GetSdkWorkloadMetadataDir(featureBand), InstalledWorkloadDir, workloadId.ToString());
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
            var path = Path.Combine(GetSdkWorkloadMetadataDir(featureBand), InstalledWorkloadDir, workloadId.ToString());
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetSdkWorkloadMetadataDir(SdkFeatureBand featureBand)
        {
            return Path.Combine(WorkloadInstall.IsUserLocal(_dotnetDir, featureBand.ToString()) ? _userWorkloadMetadataDir : _dotnetWorkloadMetadataDir, featureBand.ToString());
        }
    }
}
