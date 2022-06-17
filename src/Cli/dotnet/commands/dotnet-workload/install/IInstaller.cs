// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IInstaller : IWorkloadManifestInstaller
    {
        int ExitCode { get; }

        void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null);

        void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null);

        IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems);

        void GarbageCollectInstalledWorkloadPacks(DirectoryPath? offlineCache = null);

        void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null, bool isRollback = false);

        IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository();

        void Shutdown();

        
    }

    // Interface to pass to workload manifest updater
    internal interface IWorkloadManifestInstaller
    {
        PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand);

        //  Extract the contents of the manifest (IE what's in the data directory in the file-based NuGet package) to the targetPath
        Task ExtractManifestAsync(string nupkgPath, string targetPath);
    }

    public class WorkloadDownload
    {
        public string NuGetPackageId { get; }

        public string NuGetPackageVersion { get; }

        public WorkloadDownload(string nuGetPackageId, string nuGetPackageVersion)
        {
            NuGetPackageId = nuGetPackageId;
            NuGetPackageVersion = nuGetPackageVersion;
        }
    }
}
