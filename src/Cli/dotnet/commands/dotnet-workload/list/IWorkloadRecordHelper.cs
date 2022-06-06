using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal interface IWorkloadListHelper : IWorkloadsRepositoryEnumerator
    {
        IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; }
        IWorkloadResolver WorkloadResolver { get; }
        void CheckTargetSdkVersionIsValid();
    }
}
