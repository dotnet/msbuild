// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : InstallingWorkloadCommand
    {
        private readonly bool _adManifestOnlyOption;
        private readonly bool _printRollbackDefinitionOnly;
        private readonly bool _fromPreviousSdk;
        private readonly List<IInstaller> _installersToShutdown = new();

        public WorkloadUpdateCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string dotnetDir = null,
            string userProfileDir = null,
            string tempDirPath = null,
            string version = null)
            : base(parseResult, reporter: reporter, workloadResolver: workloadResolver, workloadInstaller: workloadInstaller,
                  nugetPackageDownloader: nugetPackageDownloader, workloadManifestUpdater: workloadManifestUpdater,
                  dotnetDir: dotnetDir, userProfileDir: userProfileDir, tempDirPath: tempDirPath, version: version)

        {
            _fromPreviousSdk = parseResult.GetValueForOption(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValueForOption(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _printRollbackDefinitionOnly = parseResult.GetValueForOption(WorkloadUpdateCommandParser.PrintRollbackOption);
        }

        protected override IInstaller CreateWorkloadInstaller(IWorkloadResolver workloadResolver = null)
        {
            var installer = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(Reporter,
                                _sdkFeatureBand, workloadResolver ?? _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader,
                                _dotnetPath, TempDirectoryPath, packageSourceLocation: _packageSourceLocation, RestoreActionConfiguration,
                                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _installersToShutdown.Add(installer);
            return installer;
        }

        protected override IWorkloadManifestUpdater CreateWorkloadManifestUpdater(IInstaller installer, IWorkloadResolver workloadResolver = null)
        {
            return _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(Reporter, workloadResolver ?? _workloadResolver, PackageDownloader, _userProfileDir, TempDirectoryPath,
                installer.GetWorkloadInstallationRecordRepository(), installer, _packageSourceLocation);
        }

        public override int Execute()
        {
            if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCacheAsync(new DirectoryPath(_downloadToCacheOption), _includePreviews).Wait();
                }
                catch (Exception e)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                }
            }
            else if (_printDownloadLinkOnly)
            {
                var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews).GetAwaiter().GetResult();

                Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
                Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (_adManifestOnlyOption)
            {
                var installer = CreateWorkloadInstaller();
                var manifestUpdater = CreateWorkloadManifestUpdater(installer);

                manifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption)).Wait();
                Reporter.WriteLine();
                Reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
            }
            else if (_printRollbackDefinitionOnly)
            {
                var manifests = _workloadResolver.GetInstalledManifests().ToDictionary(m => m.Id, m => m.Version + "/" + m.ManifestFeatureBand, StringComparer.OrdinalIgnoreCase);

                Reporter.WriteLine("==workloadRollbackDefinitionJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(manifests, new JsonSerializerOptions() { WriteIndented = true }));
                Reporter.WriteLine("==workloadRollbackDefinitionJsonOutputEnd==");
            }
            else
            {
                try
                {
                    UpdateWorkloads(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption));
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadUpdateFailed, e.Message), e, isUserError: false);
                }
            }

            int exitCode = 0;

            foreach (var installer in _installersToShutdown)
            {
                installer.Shutdown();
                exitCode = installer.ExitCode;
            }

            return exitCode;
        }

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            var installer = CreateWorkloadInstaller();
            var manifestUpdater = CreateWorkloadManifestUpdater(installer);

            Reporter.WriteLine();

            var workloadIds = GetUpdatableWorkloads(installer);
            manifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();

            var manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                manifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                manifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);

            UpdateWorkloadsWithInstallRecord(installer, workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(installer, Reporter, Verbosity, offlineCache);

            manifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private void UpdateWorkloadsWithInstallRecord(
            IInstaller installer,
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache = null)
        {

            var transaction = new CliTransaction();

            transaction.RollbackStarted = () =>
            {
                Reporter.WriteLine(LocalizableStrings.RollingBackInstall);
            };
            // Don't hide the original error if roll back fails, but do log the rollback failure
            transaction.RollbackFailed = ex =>
            {
                Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message));
            };

            transaction.Run(
                action: context =>
                {
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        installer.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    var workloads = GetUpdatableWorkloads(installer);

                    installer.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);
                },
                rollback: () =>
                {
                    //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
                });

        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews)
        {
            var installer = CreateWorkloadInstaller();

            await GetDownloads(GetUpdatableWorkloads(installer), skipManifestUpdate: false, includePreviews, offlineCache.Value);
        }

        private async Task<IEnumerable<string>> GetUpdatablePackageUrlsAsync(bool includePreview)
        {
            var installer = CreateWorkloadInstaller();

            var downloads = await GetDownloads(GetUpdatableWorkloads(installer), skipManifestUpdate: false, includePreview);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await PackageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private IEnumerable<WorkloadId> GetUpdatableWorkloads(IInstaller installer)
        {
            var currentFeatureBand = new SdkFeatureBand(_sdkVersion);
            if (_fromPreviousSdk)
            {
                var priorFeatureBands = installer.GetWorkloadInstallationRecordRepository().GetFeatureBandsWithInstallationRecords()
                    .Where(featureBand => featureBand.CompareTo(currentFeatureBand) < 0);
                if (priorFeatureBands.Any())
                {
                    var maxPriorFeatureBand = priorFeatureBands.Max();
                    return installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(maxPriorFeatureBand);
                }
                return new List<WorkloadId>();
            }
            else
            {
                var workloads = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(currentFeatureBand);
                if (workloads == null || !workloads.Any())
                {
                    Reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
                }

                return workloads;
            }
        }
    }
}
