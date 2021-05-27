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
using Microsoft.DotNet.MSBuildSdkResolver;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : CommandBase
    {
        private readonly bool _printDownloadLinkOnly;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly IReporter _reporter;
        private readonly bool _includePreviews;
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
            _downloadToCacheOption = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.DownloadToCacheOption);
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadUpdateCommandParser.VerbosityOption);
            _sdkVersion = string.IsNullOrEmpty(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.VersionOption)) ?
                new ReleaseVersion(version ?? Product.Version) :
                new ReleaseVersion(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.VersionOption));
            _tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.TempDirOption));

            var configOption = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.ConfigOption);
            var addSourceOption = parseResult.ValueForOption<string[]>(WorkloadUpdateCommandParser.AddSourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (addSourceOption == null || !addSourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides:  addSourceOption);

            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = workloadInstaller ?? WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand, _workloadResolver, _verbosity, nugetPackageDownloader,
                dotnetDir, packageSourceLocation: _packageSourceLocation);
            _userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            var tempPackagesDir = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir, filePermissionSetter: null, new NullLogger());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _nugetPackageDownloader, _userHome, _tempDirPath, _packageSourceLocation);
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
                var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews).Result;

                _reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                _reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                _reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
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
            var featureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
            var manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates();

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

                        workloadPackToUpdate = workloadIds
                            .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                            .Distinct()
                            .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                            .Where(pack => pack != null);

                        foreach (var packId in workloadPackToUpdate)
                        {
                            installer.InstallWorkloadPack(packId, sdkFeatureBand, offlineCache);
                        }

                        foreach (var workloadId in workloadIds)
                        {
                            _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                .WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
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

                            foreach (var workloadId in workloadIds)
                            {
                                _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                    .DeleteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
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
                        .Select(packInfo => _nugetPackageDownloader.GetPackageUrl(new PackageId(packInfo.ResolvedPackageId), new NuGetVersion(packInfo.Version), _packageSourceLocation).Result);
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

        private IEnumerable<PackInfo> GetUpdatablePacks(IWorkloadPackInstaller installer)
        {
            var installedPacks = installer.GetInstalledPacks(new SdkFeatureBand(_sdkVersion));
            if (installedPacks == null || !installedPacks.Any())
            {
                return new List<PackInfo>();
            }
            var updatedPacks = installer.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion))
                .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);

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
