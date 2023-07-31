// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.Install;
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

        public Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand)
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
