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
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Configurer;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
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
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir, filePermissionSetter: null, new NullLogger());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _nugetPackageDownloader, userHome);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly || !string.IsNullOrWhiteSpace(_fromCacheOption))
            {
                Reporter.Output.WriteLine($"WIP workload install {string.Join("; ", _workloadIds)}");
                List<string> allowedMockWorkloads = new List<string> {"mobile-ios", "mobile-android"};

                if (_workloadIds.Except(allowedMockWorkloads).Any())
                {
                    Reporter.Output.WriteLine("Only support \"mobile-ios\", \"mobile-android\" in the mock");
                }

                SourceRepository source =
                    Repository.Factory.GetCoreV3("https://www.myget.org/F/mockworkloadfeed/api/v3/index.json");
                ServiceIndexResourceV3 serviceIndexResource = source.GetResourceAsync<ServiceIndexResourceV3>().Result;
                IReadOnlyList<Uri> packageBaseAddress =
                    serviceIndexResource?.GetServiceEntryUris(ServiceTypes.PackageBaseAddress);
                List<string> allPackageUrl = new List<string>();

                if (_printDownloadLinkOnly)
                {
                    if (_workloadIds.Contains("mobile-ios"))
                    {
                        allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.iOS.Bundle",
                            NuGetVersion.Parse("6.0.100")));

                        AddNewtonsoftJson(allPackageUrl);
                    }

                    if (_workloadIds.Contains("mobile-android"))
                    {
                        allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.NET.Workload.Android",
                            NuGetVersion.Parse("6.0.100")));


                        AddNewtonsoftJson(allPackageUrl);
                    }

                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputStart==");
                    Reporter.Output.WriteLine(JsonSerializer.Serialize(allPackageUrl));
                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputEnd==");
                }

                if (!string.IsNullOrWhiteSpace(_fromCacheOption))
                {
                    Directory.CreateDirectory(MockInstallDirectory);
                    if (_workloadIds.Contains("mobile-android"))
                    {
                        File.Copy(Path.Combine(_fromCacheOption, "Microsoft.NET.Workload.Android.6.0.100.nupkg"),
                            Path.Combine(MockInstallDirectory, "Microsoft.NET.Workload.Android.6.0.100.nupkg"));
                    }

                    if (_workloadIds.Contains("mobile-ios"))
                    {
                        File.Copy(Path.Combine(_fromCacheOption, "Microsoft.iOS.Bundle.6.0.100.nupkg"),
                            Path.Combine(MockInstallDirectory, "Microsoft.iOS.Bundle.6.0.100.nupkg"));
                    }
                }
            }
            else
            {
                try
                {
                    InstallWorkloads(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews);
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e);
                }
            }

            return 0;
        }

        // Add a Newtonsoft.Json to make sure caller can handle multiple packages
        private static void AddNewtonsoftJson(List<string> allPackageUrl)
        {
            string newtonsoftJsonUrl = "https://www.nuget.org/api/v2/package/Newtonsoft.Json/13.0.1-beta2";
            if (!allPackageUrl.Contains(newtonsoftJsonUrl))
            {
                allPackageUrl.Add(newtonsoftJsonUrl);
            }
        }

        public string nupkgUrl(string baseUri, string id, NuGetVersion version) =>
            baseUri + id.ToLowerInvariant() + "/" + version.ToNormalizedString() + "/" + id.ToLowerInvariant() +
            "." +
            version.ToNormalizedString() + ".nupkg";

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false)
        {
            _reporter.WriteLine();
            var featureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestsToUpdate = new List<(ManifestId,  ManifestVersion, ManifestVersion)>();
            if (!skipManifestUpdate)
            {
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews).Wait();
                manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates();
            }

            InstallWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate);

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
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate)
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
    }
}
