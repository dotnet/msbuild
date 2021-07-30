// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
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
    internal class WorkloadRepairCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly VerbosityOptions _verbosity;
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
            string version = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadRepairCommandParser.VerbosityOption);
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.ValueForOption<string>(WorkloadRepairCommandParser.VersionOption), version, _dotnetPath);

            var configOption = parseResult.ValueForOption<string>(WorkloadRepairCommandParser.ConfigOption);
            var sourceOption = parseResult.ValueForOption<string[]>(WorkloadRepairCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(WorkloadInstallCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.ValueForOption<string>(WorkloadInstallCommandParser.TempDirOption));
            var tempPackagesDir = new DirectoryPath(Path.Combine(tempDirPath, "dotnet-sdk-advertising-temp"));
            NullLogger nullLogger = new NullLogger();
            nugetPackageDownloader ??= new NuGetPackageDownloader(
                tempPackagesDir,
                filePermissionSetter: null,
                new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, nullLogger), nullLogger, restoreActionConfig: _parseResult.ToRestoreActionConfig());
            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand,
                                     _workloadResolver, _verbosity, nugetPackageDownloader, dotnetDir, tempDirPath,
                                     _packageSourceLocation, _parseResult.ToRestoreActionConfig());
        }

        public override int Execute()
        {
            try
            {
                _reporter.WriteLine();

                var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));

                _reporter.WriteLine(string.Format(LocalizableStrings.RepairingWorkloads, string.Join(" ", workloadIds)));

                ReinstallWorkloadsBasedOnCurrentManifests(workloadIds, new SdkFeatureBand(_sdkVersion));

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    _workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks();
                }

                _reporter.WriteLine();
                _reporter.WriteLine(string.Format(LocalizableStrings.RepairSucceeded, string.Join(" ", workloadIds)));
                _reporter.WriteLine();
            }
            catch (Exception e)
            {
                // Don't show entire stack trace
                throw new GracefulException(string.Format(LocalizableStrings.WorkloadRepairFailed, e.Message), e);
            }
            finally
            {
                _workloadInstaller.Shutdown();
            }

            return _workloadInstaller.ExitCode;
        }

        private void ReinstallWorkloadsBasedOnCurrentManifests(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();

                var packsToInstall = workloadIds
                    .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                    .Distinct()
                    .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                    .Where(pack => pack != null);

                foreach (var packId in packsToInstall)
                {
                    installer.RepairWorkloadPack(packId, sdkFeatureBand);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
