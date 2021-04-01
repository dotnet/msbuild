// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallManager
    {
        private readonly IReporter _reporter;
        private IWorkloadInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;
        private ReleaseVersion _sdkVersion;

        public WorkloadInstallManager(
            IReporter reporter,
            IWorkloadInstaller workloadInstaller,
            IWorkloadResolver workloadResolver,
            string version = null)
        {
            _sdkVersion = new ReleaseVersion(version ?? Product.Version);
            _workloadInstaller = workloadInstaller;
            _workloadResolver = workloadResolver;
            _reporter = reporter;
        }

        public void InstallWorkloads(IEnumerable<string> workloadIds, bool skipManifestUpdate = false)
        {
            _reporter.WriteLine();
            var featureBand = string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand);

            if (!skipManifestUpdate)
            {
                throw new NotImplementedException();
            }

            InstallWorkloadComponents(workloadIds, featureBand);

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                (_workloadInstaller as PackWorkloadInstallerBase).GarbageCollectInstalledWorkloadPacks();
            }

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(", ", workloadIds)));
            _reporter.WriteLine();
        }

        private void InstallWorkloadComponents(IEnumerable<string> workloadIds, string featureBand)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller as PackWorkloadInstallerBase;

                var workloadPacksMap = workloadIds
                    .Select(workloadId => (workloadId, _workloadResolver.GetPacksInWorkload(workloadId).Select(packId => _workloadResolver.TryGetPackInfo(packId))));
                TransactionalAction.Run(
                    action: () =>
                    {
                        foreach ((var workloadId, var packsToInstall) in workloadPacksMap)
                        {
                            foreach (var packId in packsToInstall)
                            {
                                installer.InstallWorkloadPack(packId, featureBand);
                            }

                            _workloadInstaller.WriteWorkloadInstallationRecord(workloadId, featureBand);
                        }

                    },
                    rollback: () => {
                        foreach ((var workloadId, var packsToInstall) in workloadPacksMap)
                        {
                            foreach (var packId in packsToInstall)
                            {
                                installer.RollBackWorkloadPackInstall(packId, featureBand);
                            }

                            _workloadInstaller.DeleteWorkloadInstallationRecord(workloadId, featureBand);
                        }
                    });
            }
            else
            {
                var installer = _workloadInstaller as WorkloadUnitInstallerBase;
                foreach (var workloadId in workloadIds)
                {
                    installer.InstallWorkload(workloadId);
                }
            }
        }
    }
}
