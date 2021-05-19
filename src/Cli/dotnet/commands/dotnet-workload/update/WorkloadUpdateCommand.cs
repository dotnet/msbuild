// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : CommandBase
    {
        private readonly bool _printDownloadLinkOnly;
        private readonly string _fromCacheOption;
        private readonly IReporter _reporter;
        private readonly bool _includePreviews;
        private readonly VerbosityOptions _verbosity;
        private readonly IInstaller _workloadInstaller;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly IWorkloadManifestProvider _workloadManifestProvider;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;

        public static readonly string MockUpdateDirectory = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),
            "DEV_mockworkloads", "update");

        public WorkloadUpdateCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string dotnetDir = null,
            string userHome = null,
            string version = null)
            : base(parseResult)
        {
            _printDownloadLinkOnly =
                parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.FromCacheOption);
            _reporter = reporter ?? Reporter.Output;
            _includePreviews = parseResult.ValueForOption<bool>(WorkloadUpdateCommandParser.IncludePreviewsOption);
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadUpdateCommandParser.VerbosityOption);
            _sdkVersion = new ReleaseVersion(version ??
                (string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.SdkVersionOption)) ?
                Product.Version : parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.SdkVersionOption)));

            var dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(_workloadManifestProvider, dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = workloadInstaller ?? WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand, _workloadResolver, _verbosity, nugetPackageDownloader, dotnetDir);
            userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            var tempPackagesDir = new DirectoryPath(Path.Combine(userHome, ".dotnet", "sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir, filePermissionSetter: null, new NullLogger());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _nugetPackageDownloader, userHome);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly || !string.IsNullOrWhiteSpace(_fromCacheOption))
            {
                SourceRepository source =
                    Repository.Factory.GetCoreV3("https://www.myget.org/F/mockworkloadfeed/api/v3/index.json");
                ServiceIndexResourceV3 serviceIndexResource = source.GetResourceAsync<ServiceIndexResourceV3>().Result;
                IReadOnlyList<Uri> packageBaseAddress =
                    serviceIndexResource?.GetServiceEntryUris(ServiceTypes.PackageBaseAddress);
                List<string> allPackageUrl = new List<string>();

                if (_printDownloadLinkOnly)
                {
                    allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.iOS.Bundle",
                        NuGetVersion.Parse("6.0.100")));

                    allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.NET.Workload.Android",
                        NuGetVersion.Parse("6.0.100")));

                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputStart==");
                    Reporter.Output.WriteLine(JsonSerializer.Serialize(allPackageUrl));
                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputEnd==");
                }

                if (!string.IsNullOrWhiteSpace(_fromCacheOption))
                {
                    Directory.CreateDirectory(MockUpdateDirectory);

                    File.Copy(Path.Combine(_fromCacheOption, "Microsoft.NET.Workload.Android.6.0.100.nupkg"),
                        Path.Combine(MockUpdateDirectory, "Microsoft.NET.Workload.Android.6.0.100.nupkg"));

                    File.Copy(Path.Combine(_fromCacheOption, "Microsoft.iOS.Bundle.6.0.100.nupkg"),
                        Path.Combine(MockUpdateDirectory, "Microsoft.iOS.Bundle.6.0.100.nupkg"));
                }
            }
            else
            {
                UpdateWorkloads(_includePreviews);
            }

            return 0;
        }

        public string nupkgUrl(string baseUri, string id, NuGetVersion version) =>
            baseUri + id.ToLowerInvariant() + "/" + version.ToNormalizedString() + "/" + id.ToLowerInvariant() +
            "." +
            version.ToNormalizedString() + ".nupkg";

        public void UpdateWorkloads(bool includePreviews = false)
        {
            _reporter.WriteLine();
            var featureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews).Wait();
            var manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates();

            UpdateWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate);

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
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate)
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
                            _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.newVersion, sdkFeatureBand);
                        }

                        _workloadResolver.RefreshWorkloadManifests();

                        workloadPackToUpdate = workloadIds
                            .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                            .Distinct()
                            .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                            .Where(pack => pack != null);

                        foreach (var packId in workloadPackToUpdate)
                        {
                            installer.InstallWorkloadPack(packId, sdkFeatureBand);
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
    }
}
