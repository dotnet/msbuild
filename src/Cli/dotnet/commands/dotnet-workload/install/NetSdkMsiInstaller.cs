// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NetSdkMsiInstaller : IWorkloadPackInstaller
    {
        public void DownloadToOfflineCache(WorkloadResolver.PackInfo packInfo, DirectoryPath offlineCache, bool includePreviews) => throw new NotImplementedException();
        public void GarbageCollectInstalledWorkloadPacks() => throw new NotImplementedException();
        public InstallationUnit GetInstallationUnit() => throw new NotImplementedException();
        public IEnumerable<WorkloadResolver.PackInfo> GetInstalledPacks(SdkFeatureBand sdkFeatureBand) => throw new NotImplementedException();
        public IWorkloadPackInstaller GetPackInstaller() => throw new NotImplementedException();
        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository() => throw new NotImplementedException();
        public IWorkloadInstaller GetWorkloadInstaller() => throw new NotImplementedException();
        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null) => throw new NotImplementedException();
        public void InstallWorkloadPack(WorkloadResolver.PackInfo packInfo, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null) => throw new NotImplementedException();
        public void RollBackWorkloadPackInstall(WorkloadResolver.PackInfo packInfo, SdkFeatureBand sdkFeatureBand) => throw new NotImplementedException();
        IEnumerable<(string Id, string Version)> IWorkloadPackInstaller.GetInstalledPacks(SdkFeatureBand sdkFeatureBand) => throw new NotImplementedException();
    }
}
