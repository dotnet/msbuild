// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.List.Tests
{
    internal class MockWorkloadRecordRepo : IWorkloadInstallationRecordRepository
    {
        private readonly IEnumerable<WorkloadId> _workloadIds;

        public MockWorkloadRecordRepo(IEnumerable<WorkloadId> workloadIds)
        {
            _workloadIds = workloadIds;
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        {
            return _workloadIds;
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() => throw new System.NotImplementedException();
        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
    }
}
