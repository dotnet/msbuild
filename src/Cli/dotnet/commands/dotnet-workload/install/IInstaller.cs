// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IInstaller
    {
        int ExitCode { get; }

        InstallationUnit GetInstallationUnit();

        IWorkloadPackInstaller GetPackInstaller();

        IWorkloadInstaller GetWorkloadInstaller();

        void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null, bool isRollback = false);

        IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository();

        void Shutdown();
    }

    internal enum InstallationUnit {
        Workload,
        Packs
    }
}
