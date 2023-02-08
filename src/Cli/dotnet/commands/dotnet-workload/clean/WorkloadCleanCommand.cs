// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.CommandLine;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;

#nullable enable

namespace Microsoft.DotNet.Workloads.Workload.Clean
{
    internal class WorkloadCleanCommand : WorkloadCommandBase
    {
        private readonly bool _cleanAll;

        private readonly ReleaseVersion _sdkVersion;
        private readonly IInstaller _workloadInstaller;
        private readonly IWorkloadResolver _workloadResolver;

        public WorkloadCleanCommand(
            ParseResult parseResult,
            IReporter? reporter = null,
            IWorkloadResolver? workloadResolver = null,
            string? dotnetDir = null,
            string? version = null,
            string? userProfileDir = null
            ) : base(parseResult, reporter: reporter)
        {
            _cleanAll = parseResult.GetValue(WorkloadCleanCommandParser.CleanAllOption);

            string? dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            if (dotnetPath == null)
            {
                throw new GracefulException(String.Format(LocalizableStrings.InvalidWorkloadProcessPath, Environment.ProcessPath ?? "null"));
            }

            userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;

            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValue(WorkloadUninstallCommandParser.VersionOption), version, dotnetPath, userProfileDir, true);
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand, _workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, dotnetPath);
        }

        public override int Execute()
        {
            ExecuteGarbageCollection();
            return 0;
        }

        private void ExecuteGarbageCollection()
        {
            if (_cleanAll)
            {
                _workloadInstaller.GarbageCollectInstalledWorkloadPacks(cleanAllPacks: true);
            }
            else
            {
                _workloadInstaller.GarbageCollectInstalledWorkloadPacks();
            }

#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                InstalledWorkloadsCollection vsWorkloads = new();
                VisualStudioWorkloads.GetInstalledWorkloads(_workloadResolver, vsWorkloads, _cleanAll ? null : new SdkFeatureBand(_sdkVersion));
                foreach (var vsWorkload in vsWorkloads.AsEnumerable())
                {
                    Reporter.WriteLine(AnsiColorExtensions.Yellow(string.Format(LocalizableStrings.VSWorkloadNotRemoved, vsWorkload.Key)));
                }
            }
#endif
        }
    }
}
