using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadInfoHelper : IWorkloadInfoHelper
    {
        public readonly SdkFeatureBand _currentSdkFeatureBand;
        private readonly string _targetSdkVersion;

        public WorkloadInfoHelper(
            VerbosityOptions verbosity = VerbosityOptions.normal,
            string targetSdkVersion = null,
            bool? verifySignatures = null,
            IReporter reporter = null,
            IWorkloadInstallationRecordRepository workloadRecordRepo = null,
            string currentSdkVersion = null,
            string dotnetDir = null,
            string userProfileDir = null,
            IWorkloadResolver workloadResolver = null
        )
        {
            string dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            ReleaseVersion currentSdkReleaseVersion = new(currentSdkVersion ?? Product.Version);
            _currentSdkFeatureBand = new SdkFeatureBand(currentSdkReleaseVersion);

            _targetSdkVersion = targetSdkVersion;
            userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;
            var workloadManifestProvider =
                new SdkDirectoryWorkloadManifestProvider(dotnetPath,
                    string.IsNullOrWhiteSpace(_targetSdkVersion)
                        ? currentSdkReleaseVersion.ToString()
                        : _targetSdkVersion,
                    userProfileDir);
            WorkloadResolver = workloadResolver ?? NET.Sdk.WorkloadManifestReader.WorkloadResolver.Create(
                workloadManifestProvider, dotnetPath,
                currentSdkReleaseVersion.ToString(), userProfileDir);

            Installer = WorkloadInstallerFactory.GetWorkloadInstaller(reporter, _currentSdkFeatureBand,
                                     WorkloadResolver, verbosity, userProfileDir,
                                     verifySignatures ?? !SignCheck.IsDotNetSigned(),
                                     elevationRequired: false);

            WorkloadRecordRepo = workloadRecordRepo ?? Installer.GetWorkloadInstallationRecordRepository();
        }

        public IInstaller Installer { get; private init; }
        public IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; private init; }
        public IWorkloadResolver WorkloadResolver { get; private init; }

        public IEnumerable<WorkloadId> InstalledSdkWorkloadIds =>
            WorkloadRecordRepo.GetInstalledWorkloads(_currentSdkFeatureBand);

        public InstalledWorkloadsCollection AddInstalledVsWorkloads(IEnumerable<WorkloadId> sdkWorkloadIds)
        {
            InstalledWorkloadsCollection installedWorkloads = new(sdkWorkloadIds, $"SDK {_currentSdkFeatureBand}");
#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                VisualStudioWorkloads.GetInstalledWorkloads(WorkloadResolver, _currentSdkFeatureBand,
                    installedWorkloads);
            }
#endif
            return installedWorkloads;
        }

        public void CheckTargetSdkVersionIsValid()
        {
            if (!string.IsNullOrWhiteSpace(_targetSdkVersion))
            {
                if (new SdkFeatureBand(_targetSdkVersion).CompareTo(_currentSdkFeatureBand) < 0)
                {
                    throw new ArgumentException(
                        $"Version band of {_targetSdkVersion} --- {new SdkFeatureBand(_targetSdkVersion)} should not be smaller than current version band {_currentSdkFeatureBand}");
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<WorkloadResolver.WorkloadInfo> InstalledAndExtendedWorkloads
        {
            get
            {
                var installed = AddInstalledVsWorkloads(InstalledSdkWorkloadIds);

                return WorkloadResolver.GetExtendedWorkloads(
                    installed.AsEnumerable().Select(t => new WorkloadId(t.Key)));
            }
        }

    }
}
