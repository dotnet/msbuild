// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockWorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        public int UpdateAdvertisingManifestsCallCount = 0;
        public int CalculateManifestUpdatesCallCount = 0;
        public int GetManifestPackageDownloadsCallCount = 0;
        private IEnumerable<(ManifestVersionUpdate manifestUpdate,
            Dictionary<WorkloadId, WorkloadDefinition> Workloads)> _manifestUpdates;
        private string _tempDirManifestPath;

        public MockWorkloadManifestUpdater(IEnumerable<(ManifestVersionUpdate manifestUpdate,
            Dictionary<WorkloadId, WorkloadDefinition> Workloads)> manifestUpdates = null, string tempDirManifestPath = null)
        {
            _manifestUpdates = manifestUpdates ?? new List<(ManifestVersionUpdate manifestUpdate,
                Dictionary<WorkloadId, WorkloadDefinition> Workloads)>();
            _tempDirManifestPath = tempDirManifestPath;
        }

        public Task UpdateAdvertisingManifestsAsync(bool includePreview, DirectoryPath? cachePath = null)
        {
            UpdateAdvertisingManifestsCallCount++;
            return Task.CompletedTask;
        }

        public IEnumerable<(
            ManifestVersionUpdate manifestUpdate,
            Dictionary<WorkloadId, WorkloadDefinition> Workloads)> CalculateManifestUpdates()
        {
            CalculateManifestUpdatesCallCount++;
            return _manifestUpdates;
        }

        public Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews)
        {
            GetManifestPackageDownloadsCallCount++;
            return Task.FromResult<IEnumerable<WorkloadDownload>>(new List<WorkloadDownload>()
            {
                new WorkloadDownload("mock-manifest", "mock-manifest-package", "1.0.5")
            });
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath)
        {
            return _manifestUpdates.Select(t => t.manifestUpdate);
        }

        public Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync() => throw new System.NotImplementedException();
        public IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads) => throw new System.NotImplementedException();
        public void DeleteUpdatableWorkloadsFile() { }
    }
}
