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
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : WorkloadCommandBase
    {
        private readonly bool _printDownloadLinkOnly;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly bool _adManifestOnlyOption;
        private readonly bool _printRollbackDefinitionOnly;
        private readonly string _fromRollbackDefinition;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly IReporter _reporter;
        private readonly bool _includePreviews;
        private readonly bool _fromPreviousSdk;
        private readonly VerbosityOptions _verbosity;
        private readonly IInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _userProfileDir;
        private readonly string _dotnetPath;
        private readonly string _tempDirPath;

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
            : base(parseResult)
        {
            _printDownloadLinkOnly =
                parseResult.GetValueForOption(WorkloadUpdateCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValueForOption(WorkloadUpdateCommandParser.FromCacheOption);
            _reporter = reporter ?? Reporter.Output;
            _includePreviews = parseResult.GetValueForOption(WorkloadUpdateCommandParser.IncludePreviewsOption);
            _fromPreviousSdk = parseResult.GetValueForOption(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValueForOption(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _downloadToCacheOption = parseResult.GetValueForOption(WorkloadUpdateCommandParser.DownloadToCacheOption);
            _verbosity = parseResult.GetValueForOption(WorkloadUpdateCommandParser.VerbosityOption);
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValueForOption(WorkloadUpdateCommandParser.VersionOption), version, _dotnetPath, _userProfileDir);
            _tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.GetValueForOption(WorkloadUpdateCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.GetValueForOption(WorkloadUpdateCommandParser.TempDirOption));
            _printRollbackDefinitionOnly = parseResult.GetValueForOption(WorkloadUpdateCommandParser.PrintRollbackOption);
            _fromRollbackDefinition = parseResult.GetValueForOption(WorkloadUpdateCommandParser.FromRollbackFileOption);

            var configOption = parseResult.GetValueForOption(WorkloadUpdateCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption<string[]>(WorkloadUpdateCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides:  sourceOption);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            var restoreActionConfig = _parseResult.ToRestoreActionConfig();
            _workloadInstaller = workloadInstaller ?? WorkloadInstallerFactory.GetWorkloadInstaller(_reporter,
                sdkFeatureBand, _workloadResolver, _verbosity, _userProfileDir, VerifySignatures, nugetPackageDownloader,
                dotnetDir, _tempDirPath, packageSourceLocation: _packageSourceLocation, restoreActionConfig,
                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));
            var tempPackagesDir = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir,
                filePermissionSetter: null, new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger()),
                _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger(), restoreActionConfig: restoreActionConfig);
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadResolver, _nugetPackageDownloader, _userProfileDir, _tempDirPath, 
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _packageSourceLocation);
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

                _reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                _reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                _reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (_adManifestOnlyOption)
            {
                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption)).Wait();
                _reporter.WriteLine();
                _reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
            }
            else if (_printRollbackDefinitionOnly)
            {
                var manifests = _workloadResolver.GetInstalledManifests().ToDictionary(m => m.Id, m => m.Version + "/" + m.ManifestFeatureBand, StringComparer.OrdinalIgnoreCase);

                _reporter.WriteLine("==workloadRollbackDefinitionJsonOutputStart==");
                _reporter.WriteLine(JsonSerializer.Serialize(manifests));
                _reporter.WriteLine("==workloadRollbackDefinitionJsonOutputEnd==");
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

            return 0;
        }

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            _reporter.WriteLine();
            var featureBand =
                new SdkFeatureBand(_sdkVersion);

            var workloadIds = GetUpdatableWorkloads();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
            
            var manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);

            UpdateWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, _reporter, _verbosity, offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            _reporter.WriteLine();
        }

        private void UpdateWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache = null)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();
                IEnumerable<PackInfo> workloadPackToUpdate = new List<PackInfo>();

                var transaction = new CliTransaction();

                transaction.RollbackStarted = () =>
                {
                    _reporter.WriteLine(LocalizableStrings.RollingBackInstall);
                };
                // Don't hide the original error if roll back fails, but do log the rollback failure
                transaction.RollbackFailed = ex =>
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message));
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

                        workloadPackToUpdate = GetUpdatablePacks(installer);

                        installer.InstallWorkloadPacks(workloadPackToUpdate, sdkFeatureBand, context, offlineCache);
                    },
                    rollback: () => {
                        //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
                    });
            }
            else
            {
                var installer = _workloadInstaller.GetWorkloadInstaller();
                foreach (var workloadId in workloadIds)
                {
                    installer.InstallWorkload(workloadId);
                }
            }
        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews)
        {
            var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreviews, offlineCache);
            var tempManifestDir = Path.Combine(offlineCache.Value, "temp-manifests");
            try
            {
                await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, new DirectoryPath(tempManifestDir));
                var overlayManifestProvider = new TempDirectoryWorkloadManifestProvider(tempManifestDir, _sdkVersion.ToString());
                _workloadResolver = WorkloadResolver.Create(overlayManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    var installer = _workloadInstaller.GetPackInstaller();
                    var packsToUpdate = GetUpdatablePacks(installer);
                    foreach (var pack in packsToUpdate)
                    {
                        installer.DownloadToOfflineCache(pack, new DirectoryPath(_downloadToCacheOption), _includePreviews);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempManifestDir) && Directory.Exists(tempManifestDir))
                {
                   Directory.Delete(tempManifestDir, true);
                }
            }
        }

        private async Task<IEnumerable<string>> GetUpdatablePackageUrlsAsync(bool includePreview)
        {
            IEnumerable<string> packageUrls = new List<string>();
            DirectoryPath? tempPath = null;

            try
            {
                var manifestPackageUrls = _workloadManifestUpdater.GetManifestPackageUrls(includePreview);
                packageUrls = packageUrls.Concat(manifestPackageUrls);

                tempPath = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-manifest-extraction"));
                await UseTempManifestsToResolvePacksAsync(tempPath.Value, includePreview);

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    var installer = _workloadInstaller.GetPackInstaller();
                    var packsToUpdate = GetUpdatablePacks(installer)
                        .Select(packInfo => _nugetPackageDownloader.GetPackageUrl(new PackageId(packInfo.ResolvedPackageId), new NuGetVersion(packInfo.Version), _packageSourceLocation).GetAwaiter().GetResult());
                    packageUrls = packageUrls.Concat(packsToUpdate);
                    return packageUrls;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            finally
            {
                if (tempPath != null && tempPath.HasValue && Directory.Exists(tempPath.Value.Value))
                {
                    Directory.Delete(tempPath.Value.Value, true);
                }
            }
        }

        private async Task UseTempManifestsToResolvePacksAsync(DirectoryPath tempPath, bool includePreview)
        {
            var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreview, tempPath);
            await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, tempPath);
            var overlayManifestProvider = new TempDirectoryWorkloadManifestProvider(tempPath.Value, _sdkVersion.ToString());
            _workloadResolver = WorkloadResolver.Create(overlayManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
        }

        private IEnumerable<WorkloadId> GetUpdatableWorkloads()
        {
            var currentFeatureBand = new SdkFeatureBand(_sdkVersion);
            if (_fromPreviousSdk)
            {
                var priorFeatureBands = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetFeatureBandsWithInstallationRecords()
                    .Where(featureBand => featureBand.CompareTo(currentFeatureBand) < 0);
                if (priorFeatureBands.Any())
                {
                    var maxPriorFeatureBand = priorFeatureBands.Max();
                    return _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(maxPriorFeatureBand);
                }
                return new List<WorkloadId>();
            }
            else
            {
                var workloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(currentFeatureBand);
                if (workloads == null || !workloads.Any())
                {
                    _reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
                }

                return workloads;
            }
        }

        private IEnumerable<PackInfo> GetUpdatablePacks(IWorkloadPackInstaller installer)
        {
            var currentFeatureBand = new SdkFeatureBand(_sdkVersion);
            var workloads = GetUpdatableWorkloads();
            var updatedPacks = workloads.SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);
            var installedPacks = installer.GetInstalledPacks(currentFeatureBand);

            var packsToUpdate = new List<PackInfo>();
            foreach (var updatedPack in updatedPacks)
            {
                var installedPackIds = installedPacks.Select(pack => pack.Id);
                if (installedPackIds.Contains(updatedPack.Id))
                {
                    var installedPack = installedPacks.First(pack => pack.Id.Equals(updatedPack.Id));
                    var installedVersion = new ReleaseVersion(installedPack.Version);
                    var updatedVersion = new ReleaseVersion(updatedPack.Version);
                    if (installedVersion != null && updatedVersion != null && installedVersion < updatedVersion)
                    {
                        packsToUpdate.Add(updatedPack);
                    }
                }
                else
                {
                    // New pack required for this workload, include it
                    packsToUpdate.Add(updatedPack);
                }
            }

            return packsToUpdate;
        }
    }
}
