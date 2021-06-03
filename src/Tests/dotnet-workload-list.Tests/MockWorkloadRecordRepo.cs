// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
