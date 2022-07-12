// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : InstallingWorkloadCommand
    {
        private readonly bool _skipManifestUpdate;
        private readonly IReadOnlyCollection<string> _workloadIds;

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string dotnetDir = null,
            string userProfileDir = null,
            string tempDirPath = null,
            string version = null,
            IReadOnlyCollection<string> workloadIds = null)
            : base(parseResult, reporter: reporter, workloadResolver: workloadResolver, workloadInstaller: workloadInstaller,
                  nugetPackageDownloader: nugetPackageDownloader, workloadManifestUpdater: workloadManifestUpdater,
                  dotnetDir: dotnetDir, userProfileDir: userProfileDir, tempDirPath: tempDirPath, version: version)
        {
            _skipManifestUpdate = parseResult.GetValueForOption(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _workloadIds = workloadIds ?? parseResult.GetValueForArgument(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();

            _workloadInstaller = _workloadInstallerFromConstructor ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand,
                                     workloadResolver ?? _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, RestoreActionConfiguration, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            bool displayManifestUpdates = false;
            if (Verbosity.VerbosityIsDetailedOrDiagnostic())
            {
                displayManifestUpdates = true;
            }
            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(Reporter, workloadResolver ?? _workloadResolver, PackageDownloader, _userProfileDir, TempDirectoryPath,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), (IWorkloadManifestInstaller)_workloadInstaller, _packageSourceLocation, displayManifestUpdates: displayManifestUpdates);


            ValidateWorkloadIdsInput();
        }

        private void ValidateWorkloadIdsInput()
        {
            var availableWorkloads = _workloadResolver.GetAvailableWorkloads();
            foreach (var workloadId in _workloadIds)
            {
                if (!availableWorkloads.Select(workload => workload.Id.ToString()).Contains(workloadId))
                {
                    if (_workloadResolver.IsPlatformIncompatibleWorkload(new WorkloadId(workloadId)))
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadNotSupportedOnPlatform, workloadId), isUserError: false);
                    }
                    else
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadNotRecognized, workloadId), isUserError: false);
                    }
                }
            }
        }

        public override int Execute()
        {
            bool usedRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);
            if (_printDownloadLinkOnly)
            {
                Reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));

                //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                //  which require new packs for currently installed workloads, those packs will be downloaded.
                //  If the packs are already installed, they won't be included in the results
                var existingWorkloads = GetInstalledWorkloads(false);
                var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                var packageUrls = GetPackageDownloadUrlsAsync(workloadsToDownload, _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();

                Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
                Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                    //  which require new packs for currently installed workloads, those packs will be downloaded.
                    //  If the packs are already installed, they won't be included in the results
                    var existingWorkloads = GetInstalledWorkloads(false);
                    var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                    DownloadToOfflineCacheAsync(workloadsToDownload, new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
                }
                catch (Exception e)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                }
            }
            else if (_skipManifestUpdate && usedRollback)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CannotCombineSkipManifestAndRollback, 
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, InstallingWorkloadCommandParser.FromRollbackFileOption.Name,
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, InstallingWorkloadCommandParser.FromRollbackFileOption.Name), isUserError: true);
            }
            else
            {
                try
                {
                    InstallWorkloads(
                        _workloadIds.Select(id => new WorkloadId(id)),
                        _skipManifestUpdate,
                        _includePreviews,
                        string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption));
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e, isUserError: false);
                }
            }

            _workloadInstaller.Shutdown();
            return _workloadInstaller.ExitCode;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            Reporter.WriteLine();

            var manifestsToUpdate = Enumerable.Empty<ManifestVersionUpdate> ();
            if (!skipManifestUpdate)
            {
                if (Verbosity != VerbosityOptions.quiet && Verbosity != VerbosityOptions.q)
                {
                    Reporter.WriteLine(LocalizableStrings.CheckForUpdatedWorkloadManifests);
                }
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                if (previouslyInstalledWorkloads.Any())
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                }
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                    _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                    _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);
            }

            InstallWorkloadsWithInstallRecord(_workloadInstaller, workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, offlineCache);

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        internal static void TryRunGarbageCollection(IInstaller workloadInstaller, IReporter reporter, VerbosityOptions verbosity, DirectoryPath? offlineCache = null)
        {
            try
            {
                workloadInstaller.GarbageCollectInstalledWorkloadPacks(offlineCache);
            }
            catch (Exception e)
            {
                // Garbage collection failed, warn user
                reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectionFailed,
                    verbosity.VerbosityIsDetailedOrDiagnostic() ? e.StackTrace.ToString() : e.Message).Yellow());
            }
        }

        private void InstallWorkloadsWithInstallRecord(
            IInstaller installer,
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache)
        {
            IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();
            IEnumerable<WorkloadId> newWorkloadInstallRecords = new List<WorkloadId>();

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

                    installer.InstallWorkloads(workloadIds, sdkFeatureBand, context, offlineCache);

                    var recordRepo = installer.GetWorkloadInstallationRecordRepository();
                    newWorkloadInstallRecords = workloadIds.Except(recordRepo.GetInstalledWorkloads(sdkFeatureBand));
                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        recordRepo.WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }
                },
                rollback: () =>
                {
                    //  InstallWorkloadManifest and InstallWorkloadPacks already handle rolling back their actions, so here we only
                    //  need to delete the installation records

                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        installer.GetWorkloadInstallationRecordRepository()
                            .DeleteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }
                });

        }

        private async Task<IEnumerable<string>> GetPackageDownloadUrlsAsync(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview)
        {
            var downloads = await GetDownloads(workloadIds, skipManifestUpdate, includePreview);
            
            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await PackageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            return GetDownloads(workloadIds, skipManifestUpdate, includePreviews, offlineCache.Value);
        }
    }
}
