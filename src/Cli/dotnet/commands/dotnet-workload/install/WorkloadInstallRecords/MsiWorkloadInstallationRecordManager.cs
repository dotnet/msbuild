// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
#pragma warning disable CA1416
    internal class MsiWorkloadInstallationRecordManager : IWorkloadInstallationRecordRepository
    {
        /// <summary>
        /// The base path of workload installation records in the registry.
        /// </summary>
        internal readonly string BasePath = @"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone";

        private RegistryKey _baseKey = Registry.LocalMachine;

        internal MsiWorkloadInstallationRecordManager()
        {

        }

        /// <summary>
        /// Constructor for testing purposes to allow changing the base key from HKLM.
        /// </summary>
        /// <param name="baseKey">The base key to use, e.g. <see cref="Registry.CurrentUser"/>.</param>
        internal MsiWorkloadInstallationRecordManager(RegistryKey baseKey, string basePath)
        {
            _baseKey = baseKey;
            BasePath = basePath;
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            _baseKey.DeleteSubKeyTree(Path.Combine(BasePath, $"{sdkFeatureBand}", $"{workloadId}"), throwOnMissingSubKey: false);
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            using RegistryKey key = _baseKey.OpenSubKey(BasePath);

            // ToList() is needed to ensure deferred execution does not reference closed registry keys.
            return (from string name in key.GetSubKeyNames()
                    let subkey = key.OpenSubKey(name)
                    where subkey.GetSubKeyNames().Length > 0
                    select new SdkFeatureBand(name)).ToList();
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        {
            using RegistryKey wrk = _baseKey.OpenSubKey(Path.Combine(BasePath, $"{sdkFeatureBand}"));

            // ToList() is needed to ensure deferred execution does not reference closed registry keys.
            return wrk?.GetSubKeyNames().Select(id => new WorkloadId(id)).ToList() ?? Enumerable.Empty<WorkloadId>();
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            using RegistryKey wrk = _baseKey.CreateSubKey(Path.Combine(BasePath, $"{sdkFeatureBand}", $"{workloadId}"));
        }
    }
#pragma warning restore CA1416
}
