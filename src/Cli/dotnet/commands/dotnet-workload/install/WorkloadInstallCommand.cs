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
    internal class WorkloadInstallCommand : WorkloadCommandBase
    {
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly bool _printDownloadLinkOnly;
        private readonly bool _includePreviews;
        private readonly IReadOnlyCollection<string> _workloadIds;
        private readonly IInstaller _workloadInstallerFromConstructor;
        private IWorkloadResolver _workloadResolver;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdaterFromConstructor;
        private readonly ReleaseVersion _sdkVersion;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly string _userProfileDir;
        private readonly string _dotnetPath;
        private readonly string _fromRollbackDefinition;
        private readonly List<IInstaller> _installersToShutdown = new();

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
            : base(parseResult, reporter: reporter, tempDirPath: tempDirPath, nugetPackageDownloader: nugetPackageDownloader)
        {
            _skipManifestUpdate = parseResult.GetValueForOption(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _includePreviews = parseResult.GetValueForOption(WorkloadInstallCommandParser.IncludePreviewOption);
            _printDownloadLinkOnly = parseResult.GetValueForOption(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromCacheOption);
            _downloadToCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.DownloadToCacheOption);
            _workloadIds = workloadIds ?? parseResult.GetValueForArgument(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValueForOption(WorkloadInstallCommandParser.VersionOption), version, _dotnetPath, _userProfileDir);
            _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _fromRollbackDefinition = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromRollbackFileOption);

            var configOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(sdkWorkloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            
            _workloadInstallerFromConstructor = workloadInstaller;
            _workloadManifestUpdaterFromConstructor = workloadManifestUpdater;

            ValidateWorkloadIdsInput();
        }

        IInstaller CreateWorkloadInstaller()
        {
            var installer = _workloadInstallerFromConstructor ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand,
                                     _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, RestoreActionConfiguration, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _installersToShutdown.Add(installer);
            return installer;
        }

        IWorkloadManifestUpdater CreateWorkloadManifestUpdater(IInstaller installer)
        {
            bool displayManifestUpdates = false;
            if (Verbosity.VerbosityIsDetailedOrDiagnostic())
            {
                displayManifestUpdates = true;
            }
            return _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(Reporter, _workloadResolver, PackageDownloader, _userProfileDir, TempDirectoryPath,
                installer.GetWorkloadInstallationRecordRepository(), _packageSourceLocation, displayManifestUpdates: displayManifestUpdates);
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
                var packageUrls = GetPackageDownloadUrlsAsync(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();

                Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCacheAsync(_workloadIds.Select(id => new WorkloadId(id)), new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
                }
                catch (Exception e)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                }
            }
            else if (_skipManifestUpdate && usedRollback)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CannotCombineSkipManifestAndRollback, 
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, WorkloadInstallCommandParser.FromRollbackFileOption.Name,
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, WorkloadInstallCommandParser.FromRollbackFileOption.Name), isUserError: true);
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

            int exitCode = 0;

            foreach (var installer in _installersToShutdown)
            {
                installer.Shutdown();
                exitCode = installer.ExitCode;
            }

            return exitCode;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            Reporter.WriteLine();

            var installer = CreateWorkloadInstaller();
            var manifestUpdater = CreateWorkloadManifestUpdater(installer);

            var manifestsToUpdate = Enumerable.Empty<ManifestVersionUpdate> ();
            if (!skipManifestUpdate)
            {
                if (Verbosity != VerbosityOptions.quiet && Verbosity != VerbosityOptions.q)
                {
                    Reporter.WriteLine(LocalizableStrings.CheckForUpdatedWorkloadManifests);
                }
                // Update currently installed workloads
                var installedWorkloads = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                if (previouslyInstalledWorkloads.Any())
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                }
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                manifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                    manifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                    manifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);
            }

            InstallWorkloadsWithInstallRecord(installer, workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            TryRunGarbageCollection(installer, Reporter, Verbosity, offlineCache);

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
            var packageUrls = new List<string>();
            DirectoryPath? tempPath = null;

            var installer = CreateWorkloadInstaller();
            var manifestUpdater = CreateWorkloadManifestUpdater(installer);

            try
            {
                if (!skipManifestUpdate)
                {
                    var manifestPackageUrls = manifestUpdater.GetManifestPackageUrls(includePreview);
                    packageUrls.AddRange(manifestPackageUrls);

                    tempPath = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-manifest-extraction"));
                    await UseTempManifestsToResolvePacksAsync(manifestUpdater, tempPath.Value, includePreview);

                    //  Create new installer with the updated resolver
                    installer = CreateWorkloadInstaller();

                    var installedWorkloads = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
                }

                var packUrls = installer.GetDownloads(workloadIds, _sdkFeatureBand, false)
                    .Select(pack => PackageDownloader.GetPackageUrl(new PackageId(pack.NuGetPackageId), new NuGetVersion(pack.NuGetPackageVersion),
                        packageSourceLocation: _packageSourceLocation, includePreview: includePreview).GetAwaiter().GetResult());
                packageUrls.AddRange(packUrls);

                return packageUrls;
            }
            finally
            {
                if (tempPath != null && tempPath.HasValue && Directory.Exists(tempPath.Value.Value))
                {
                    Directory.Delete(tempPath.Value.Value, true);
                }
            }
        }

        private async Task UseTempManifestsToResolvePacksAsync(IWorkloadManifestUpdater manifestUpdater, DirectoryPath tempPath, bool includePreview)
        {
            var manifestPackagePaths = await manifestUpdater.DownloadManifestPackagesAsync(includePreview, tempPath);
            if (manifestPackagePaths == null || !manifestPackagePaths.Any())
            {
                Reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
                return;
            }
            await manifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, tempPath);
            var overlayProvider = new TempDirectoryWorkloadManifestProvider(tempPath.Value, _sdkVersion.ToString());
            _workloadResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
        }

        private async Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            var installer = CreateWorkloadInstaller();
            var manifestUpdater = CreateWorkloadManifestUpdater(installer);

            string tempManifestDir = null;
            if (!skipManifestUpdate)
            {
                var manifestPackagePaths = await manifestUpdater.DownloadManifestPackagesAsync(includePreviews, offlineCache);
                if (manifestPackagePaths != null && manifestPackagePaths.Any())
                {
                    tempManifestDir = Path.Combine(offlineCache.Value, "temp-manifests");
                    await manifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, new DirectoryPath(tempManifestDir));
                    var overlayProvider = new TempDirectoryWorkloadManifestProvider(tempManifestDir, _sdkVersion.ToString());
                    _workloadResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);

                    //  Create new installer with the updated resolver
                    installer = CreateWorkloadInstaller();
                }
                else
                {
                    Reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
                }

                var installedWorkloads = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
            }

            var downloads = installer.GetDownloads(workloadIds, _sdkFeatureBand, false);

            if (!Directory.Exists(offlineCache.Value))
            {
                Directory.CreateDirectory(offlineCache.Value);
            }

            foreach (var download in downloads)
            {
                Reporter.WriteLine(string.Format(LocalizableStrings.DownloadingPackToCacheMessage, download.NuGetPackageId, download.NuGetPackageVersion, offlineCache.Value));
                
                PackageDownloader.DownloadPackageAsync(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), downloadFolder: offlineCache,
                    packageSourceLocation: _packageSourceLocation, includePreview: includePreviews).Wait();
            }

            if (!string.IsNullOrWhiteSpace(tempManifestDir) && Directory.Exists(tempManifestDir))
            {
                Directory.Delete(tempManifestDir, true);
            }
        }
    }
}
