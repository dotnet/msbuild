// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : InstallingWorkloadCommand
    {
        private readonly bool _adManifestOnlyOption;
        private readonly bool _printRollbackDefinitionOnly;
        private readonly bool _fromPreviousSdk;

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
            string version = null,
            string installedFeatureBand = null)
            : base(parseResult, reporter: reporter, workloadResolver: workloadResolver, workloadInstaller: workloadInstaller,
                  nugetPackageDownloader: nugetPackageDownloader, workloadManifestUpdater: workloadManifestUpdater,
                  dotnetDir: dotnetDir, userProfileDir: userProfileDir, tempDirPath: tempDirPath, version: version, installedFeatureBand: installedFeatureBand)

        {
            _fromPreviousSdk = parseResult.GetValue(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValue(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _printRollbackDefinitionOnly = parseResult.GetValue(WorkloadUpdateCommandParser.PrintRollbackOption);
            var resolvedReporter = _printDownloadLinkOnly || _printRollbackDefinitionOnly ? NullReporter.Instance : Reporter;

            _workloadInstaller = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(resolvedReporter,
                                _sdkFeatureBand, workloadResolver ?? _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader,
                                _dotnetPath, TempDirectoryPath, packageSourceLocation: _packageSourceLocation, RestoreActionConfiguration,
                                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(resolvedReporter, workloadResolver ?? _workloadResolver, PackageDownloader, _userProfileDir, TempDirectoryPath,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, sdkFeatureBand: _sdkFeatureBand);
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
                var packageDownloader = IsPackageDownloaderProvided ? PackageDownloader : new NuGetPackageDownloader(
                    TempPackagesDirectory,
                    filePermissionSetter: null,
                    new FirstPartyNuGetPackageSigningVerifier(),
                    new NullLogger(),
                    NullReporter.Instance,
                    restoreActionConfig: RestoreActionConfiguration,
                    verifySignatures: VerifySignatures);

                var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews, NullReporter.Instance, packageDownloader).GetAwaiter().GetResult();
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
            }
            else if (_adManifestOnlyOption)
            {
                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption)).Wait();
                Reporter.WriteLine();
                Reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
            }
            else if (_printRollbackDefinitionOnly)
            {
                var workloadSet = WorkloadSet.FromManifests(_workloadResolver.GetInstalledManifests());
                Reporter.WriteLine(workloadSet.ToJson());
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

            _workloadInstaller.Shutdown();
            return _workloadInstaller.ExitCode;
        }

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            Reporter.WriteLine();

            var workloadIds = GetUpdatableWorkloads();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();

            var manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);

            UpdateWorkloadsWithInstallRecord(workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private void UpdateWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache = null)
        {

            var transaction = new CliTransaction
            {
                RollbackStarted = () =>
                {
                    Reporter.WriteLine(LocalizableStrings.RollingBackInstall);
                },
                // Don't hide the original error if roll back fails, but do log the rollback failure
                RollbackFailed = ex =>
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message));
                }
            };

            transaction.Run(
                action: context =>
                {
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    var workloads = GetUpdatableWorkloads();

                    _workloadInstaller.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);
                },
                rollback: () =>
                {
                    //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
                });
        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews)
        {
            await GetDownloads(GetUpdatableWorkloads(), skipManifestUpdate: false, includePreviews, offlineCache.Value);
        }

        private async Task<IEnumerable<string>> GetUpdatablePackageUrlsAsync(bool includePreview, IReporter reporter = null, INuGetPackageDownloader packageDownloader = null)
        {
            reporter ??= Reporter;
            packageDownloader ??= PackageDownloader;
            var downloads = await GetDownloads(GetUpdatableWorkloads(reporter), skipManifestUpdate: false, includePreview, reporter: reporter, packageDownloader: packageDownloader);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await packageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private IEnumerable<WorkloadId> GetUpdatableWorkloads(IReporter reporter = null)
        {
            reporter ??= Reporter;
            var workloads = GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
        }
    }
}
