// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadManifestUpdater
    {
        Task UpdateAdvertisingManifestsAsync(bool includePreviews, DirectoryPath? offlineCache = null);

        Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();

        IEnumerable<(
            ManifestVersionUpdate manifestUpdate,
            Dictionary<WorkloadId, WorkloadDefinition> Workloads
            )> CalculateManifestUpdates();

        IEnumerable<ManifestVersionUpdate>
            CalculateManifestRollbacks(string rollbackDefinitionFilePath);

        Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand);

        IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads);

        void DeleteUpdatableWorkloadsFile();
    }
}
