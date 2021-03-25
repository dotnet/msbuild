// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadInstaller
    {
        InstallationUnit GetInstallationUnit();

        void InstallWorkloadManifest(string manifestId, string manifestVersion, string sdkFeatureBand);

        IReadOnlyCollection<string> GetInstalledWorkloads(string featureBand);

        void WriteWorkloadInstallationRecord(string workloadId, string featureBand);

        void DeleteWorkloadInstallationRecord(string workloadId, string featureBand);

        IReadOnlyCollection<string> GetFeatureBandsWithInstallationRecords();
    }

    internal enum InstallationUnit {
        Workload,
        Packs
    }
}
