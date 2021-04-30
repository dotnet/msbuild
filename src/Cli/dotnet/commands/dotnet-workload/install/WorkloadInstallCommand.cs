// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly bool _printDownloadLinkOnly;
        private readonly bool _includePreviews;
        private readonly VerbosityOptions _verbosity;
        private readonly IReadOnlyCollection<string> _workloadIds; 
        private readonly IInstaller _workloadInstaller;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly IWorkloadManifestProvider _workloadManifestProvider;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;

        public readonly string MockInstallDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath,
            "DEV_mockworkloads");

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string userHome = null,
            string version = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _skipManifestUpdate = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _includePreviews = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.IncludePreviewOption);
            _printDownloadLinkOnly = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.FromCacheOption);
            _downloadToCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.DownloadToCacheOption);
            _workloadIds = parseResult.ValueForArgument<IEnumerable<string>>(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadInstallCommandParser.VerbosityOption);
            _sdkVersion = new ReleaseVersion(version ?? Product.Version);

            var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(_workloadManifestProvider, dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = workloadInstaller ?? WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand, _workloadResolver, _verbosity);
            userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            var tempPackagesDir = new DirectoryPath(Path.Combine(userHome, ".dotnet", "sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir, new NullLogger());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _nugetPackageDownloader, userHome);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));
                var packageUrls = GetPackageDownloadUrls(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews);

                _reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                _reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                _reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
			else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCache(_workloadIds.Select(id => new WorkloadId(id)), _downloadToCacheOption);
                }
                catch (Exception e)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e);
                }
            }
            else
            {
                try
                {
                    InstallWorkloads(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews, _fromCacheOption);
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e);
                }
            }

            return 0;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, string offlineCache = null)
        {
            _reporter.WriteLine();
            var featureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestsToUpdate = new List<(ManifestId,  ManifestVersion, ManifestVersion)>();
            if (!skipManifestUpdate)
            {
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(featureBand, includePreviews).Wait();
                manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates(featureBand);
            }

            InstallWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate, offlineCache);

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                _workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks();
            }

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            _reporter.WriteLine();
        }

        private void InstallWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate,
			string offlineCache)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();
                IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();

                TransactionalAction.Run(
                    action: () =>
                    {
                        foreach (var manifest in manifestsToUpdate)
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.newVersion, sdkFeatureBand);
                        }

                        _workloadResolver.RefreshWorkloadManifests();

                        workloadPackToInstall = workloadIds
                            .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                            .Distinct()
                            .Select(packId => _workloadResolver.TryGetPackInfo(packId));

                        foreach (var packId in workloadPackToInstall)
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
							
                            foreach (var packId in workloadPackToInstall)
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

        private IEnumerable<string> GetPackageDownloadUrls(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview)
        {
            var packageUrls = new List<string>();
            if (!skipManifestUpdate)
            {
                packageUrls.AddRange(_workloadManifestUpdater.GetManifestsUrls(new SdkFeatureBand(_sdkVersion), includePreview));
            }

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();

                var packUrls = workloadIds
                    .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                    .Distinct()
                    .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                    .Select(pack => _nugetPackageDownloader.GetPackageUrl(new PackageId(pack.ResolvedPackageId), new NuGetVersion(pack.Version), includePreview: includePreview).Result);
                packageUrls.AddRange(packUrls);
            }
            else
            {
                throw new NotImplementedException();
            }

            return packageUrls;
        }
		
		private void DownloadToOfflineCache(IEnumerable<WorkloadId> workloadIds, string offlineCache)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();

                var workloadPacks = workloadIds
                    .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                    .Distinct()
                    .Select(packId => _workloadResolver.TryGetPackInfo(packId));

                foreach (var pack in workloadPacks)
                {
                    installer.DownloadToOfflineCache(pack, offlineCache);
                }
            }
            else
            {
                var installer = _workloadInstaller.GetWorkloadInstaller();
                foreach (var workloadId in workloadIds)
                {
                    installer.DownloadToOfflineCache(workloadId, offlineCache);
                }
            }
        }
    }
}
