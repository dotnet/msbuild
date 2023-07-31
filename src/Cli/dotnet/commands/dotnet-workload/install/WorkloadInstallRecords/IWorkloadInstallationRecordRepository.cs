// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    internal interface IWorkloadInstallationRecordRepository
    {
        IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand);

        void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);

        void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand);

        IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords();
    }
}
