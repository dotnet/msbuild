// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal interface IWorkloadInfoHelper : IWorkloadsRepositoryEnumerator
    {
        IInstaller Installer { get; }
        IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; }
        IWorkloadResolver WorkloadResolver { get; }
        void CheckTargetSdkVersionIsValid();
    }
}
