// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Workloads.Workload.Install;
using NuGet.Common;

namespace Microsoft.DotNet.Workloads.Workload.Repair
{
    internal class WorkloadRepairCommand : WorkloadCommandBase
    {
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly IInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _dotnetPath;

        public WorkloadRepairCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null,
            string version = null,
            string userProfileDir = null)
            : base(parseResult, reporter: reporter, nugetPackageDownloader: nugetPackageDownloader)
        {
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValueForOption(WorkloadRepairCommandParser.VersionOption), version, _dotnetPath, userProfileDir);

            var configOption = parseResult.GetValueForOption(WorkloadRepairCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption(WorkloadRepairCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), userProfileDir);
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            
            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand,
                                     _workloadResolver, Verbosity, userProfileDir, VerifySignatures, PackageDownloader, dotnetDir, TempDirectoryPath,
                                     _packageSourceLocation, _parseResult.ToRestoreActionConfig());
        }

        public override int Execute()
        {
            try
            {
                Reporter.WriteLine();

                var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));

                if (!workloadIds.Any())
                {
                    Reporter.WriteLine(LocalizableStrings.NoWorkloadsToRepair);
                    return 0;
                }

                Reporter.WriteLine(string.Format(LocalizableStrings.RepairingWorkloads, string.Join(" ", workloadIds)));

                ReinstallWorkloadsBasedOnCurrentManifests(workloadIds, new SdkFeatureBand(_sdkVersion));

                WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity);

                Reporter.WriteLine();
                Reporter.WriteLine(string.Format(LocalizableStrings.RepairSucceeded, string.Join(" ", workloadIds)));
                Reporter.WriteLine();
            }
            catch (Exception e)
            {
                // Don't show entire stack trace
                throw new GracefulException(string.Format(LocalizableStrings.WorkloadRepairFailed, e.Message), e, isUserError: false);
            }
            finally
            {
                _workloadInstaller.Shutdown();
            }

            return _workloadInstaller.ExitCode;
        }

        private void ReinstallWorkloadsBasedOnCurrentManifests(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand)
        {
            _workloadInstaller.RepairWorkloads(workloadIds, sdkFeatureBand);
        }
    }
}
