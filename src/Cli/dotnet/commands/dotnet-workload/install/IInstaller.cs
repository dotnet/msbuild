// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IInstaller : IWorkloadInstallationRecordInstaller
    {
        InstallationUnit GetInstallationUnit();

        IWorkloadPackInstaller GetPackInstaller();

        IWorkloadInstaller GetWorkloadInstaller();

        void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand);
    }

    internal enum InstallationUnit {
        Workload,
        Packs
    }
}
