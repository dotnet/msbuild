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
    internal class WorkloadUpdateCommand : CommandBase
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
        private IWorkloadManifestProvider _workloadManifestProvider;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _userHome;
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
            string userHome = null,
            string tempDirPath = null,
            string version = null)
            : base(parseResult)
        {
            _printDownloadLinkOnly =
                parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.FromCacheOption);
            _reporter = reporter ?? Reporter.Output;
            _includePreviews = parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.IncludePreviewsOption);
            _fromPreviousSdk = parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _downloadToCacheOption = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.DownloadToCacheOption);
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadUpdateCommandParser.VerbosityOption);
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.VersionOption), version, _dotnetPath);
            _tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.TempDirOption));
            _printRollbackDefinitionOnly = parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.PrintRollbackOption);
            _fromRollbackDefinition = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.FromRollbackFileOption);

            var configOption = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.ConfigOption);
            var sourceOption = parseResult.ValueForOption<string[]>(WorkloadUpdateCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides:  sourceOption);

            _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            var restoreActionConfig = _parseResult.ToRestoreActionConfig();
            _workloadInstaller = workloadInstaller ?? WorkloadInstallerFactory.GetWorkloadInstaller(_reporter,
                sdkFeatureBand, _workloadResolver, _verbosity, nugetPackageDownloader,
                dotnetDir, _tempDirPath, packageSourceLocation: _packageSourceLocation, restoreActionConfig,
                elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));
            _userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            var tempPackagesDir = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir,
                filePermissionSetter: null, new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger()),
                _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger(), restoreActionConfig: restoreActionConfig);
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _workloadResolver, _nugetPackageDownloader, _userHome, _tempDirPath, _packageSourceLocation);
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
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e);
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
                var manifests = _workloadResolver.GetInstalledManifests().ToDictionary(m => m.Id, m => m.Version, StringComparer.OrdinalIgnoreCase);

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
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadUpdateFailed, e.Message), e);
                }
            }

            return 0;
        }

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            _reporter.WriteLine();
            var featureBand =
                new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            var workloadIds = GetUpdatableWorkloads();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
            
            var manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                _workloadManifestUpdater.CalculateManifestUpdates().Select(m => (m.manifestId, m.existingVersion, m.newVersion)) :
                _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);

            UpdateWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate, offlineCache);

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                _workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks();
            }

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            _reporter.WriteLine();
        }

        private void UpdateWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate,
            DirectoryPath? offlineCache = null)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();
                IEnumerable<PackInfo> workloadPackToUpdate = new List<PackInfo>();

                TransactionalAction.Run(
                    action: () =>
                    {
                        foreach (var manifest in manifestsToUpdate)
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.newVersion, sdkFeatureBand, offlineCache);
                        }

                        _workloadResolver.RefreshWorkloadManifests();

                        workloadPackToUpdate = GetUpdatablePacks(installer);

                        foreach (var packId in workloadPackToUpdate)
                        {
                            installer.InstallWorkloadPack(packId, sdkFeatureBand, offlineCache);
                        }
                    },
                    rollback: () => {
                        try
                        {
                            _reporter.WriteLine(LocalizableStrings.RollingBackInstall);

                            foreach (var manifest in manifestsToUpdate)
                            {
                                _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.existingVersion, sdkFeatureBand);
                            }

                            foreach (var packId in workloadPackToUpdate)
                            {
                                installer.RollBackWorkloadPackInstall(packId, sdkFeatureBand);
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't hide the original error if roll back fails
                            _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, e.Message));
                        }
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
                _workloadManifestProvider = new TempDirectoryWorkloadManifestProvider(tempManifestDir, _sdkVersion.ToString());
                _workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());

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
            _workloadManifestProvider = new TempDirectoryWorkloadManifestProvider(tempPath.Value, _sdkVersion.ToString());
            _workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
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
