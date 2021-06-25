// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using Microsoft.DotNet.Configurer;
using NuGet.Common;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        private readonly IReporter _reporter;
        private readonly IWorkloadManifestProvider _workloadManifestProvider;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly string _userHome;
        private readonly string _tempDirPath;
        private readonly PackageSourceLocation _packageSourceLocation;
        Func<string, string> _getEnvironmentVariable;

        public WorkloadManifestUpdater(IReporter reporter,
            IWorkloadManifestProvider workloadManifestProvider,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader,
            string userHome,
            string tempDirPath,
            PackageSourceLocation packageSourceLocation = null,
            Func<string, string> getEnvironmentVariable = null)
        {
            _reporter = reporter;
            _workloadManifestProvider = workloadManifestProvider;
            _workloadResolver = workloadResolver;
            _userHome = userHome;
            _tempDirPath = tempDirPath;
            _nugetPackageDownloader = nugetPackageDownloader;
            _sdkFeatureBand = new SdkFeatureBand(_workloadManifestProvider.GetSdkFeatureBand());
            _packageSourceLocation = packageSourceLocation;
            _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        }

        public async Task UpdateAdvertisingManifestsAsync(bool includePreviews, DirectoryPath? offlineCache = null)
        {
            var manifests = GetInstalledManifestIds();
            await Task.WhenAll(manifests.Select(manifest => UpdateAdvertisingManifestAsync(manifest, includePreviews, offlineCache)))
                .ConfigureAwait(false);
        }

        public async static Task BackgroundUpdateAdvertisingManifestsAsync()
        {
            try
            {
                var reporter = new NullReporter();
                var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
                var sdkVersion = Product.Version;
                var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion);
                var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion);
                var tempPackagesDir = new DirectoryPath(Path.Combine(Path.GetTempPath(), "dotnet-sdk-advertising-temp"));
                var nugetPackageDownloader = new NuGetPackageDownloader(tempPackagesDir,
                                              filePermissionSetter: null,
                                              new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, new NullLogger()),
                                              new NullLogger(),
                                              reporter);
                var userHome = CliFolderPathCalculator.DotnetHomePath;

                var manifestUpdater = new WorkloadManifestUpdater(reporter, workloadManifestProvider, workloadResolver, nugetPackageDownloader, userHome, tempPackagesDir.Value);
                await manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();
            }
            catch (Exception)
            {
                // Never surface messages on background updates
            }
}

        public async Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync()
        {
            if (!BackgroundUpdatesAreDisabled() &&
                AdManifestSentinalIsDueForUpdate() &&
                UpdatedAdManifestPackagesExistAsync().GetAwaiter().GetResult())
            {
                await UpdateAdvertisingManifestsAsync(false);
                var sentinalPath = GetAdvertisingManifestSentinalPath();
                if (File.Exists(sentinalPath))
                {
                    File.SetLastAccessTime(sentinalPath, DateTime.Now);
                }
                else
                {
                    File.Create(sentinalPath);
                }
            }
        }

        public IEnumerable<(
            ManifestId manifestId, 
            ManifestVersion existingVersion, 
            ManifestVersion newVersion,
            Dictionary<WorkloadId, WorkloadDefinition> Workloads)> CalculateManifestUpdates()
        {
            var manifestUpdates =
                new List<(ManifestId, ManifestVersion, ManifestVersion,
                    Dictionary<WorkloadId, WorkloadDefinition> Workloads)>();
            var currentManifestIds = GetInstalledManifestIds();
            foreach (var manifestId in currentManifestIds)
            {
                var currentManifestVersion = GetInstalledManifestVersion(manifestId);
                var advertisingManifestVersionAndWorkloads = GetAdvertisingManifestVersionAndWorkloads(manifestId);
                if (advertisingManifestVersionAndWorkloads == null)
                {
                    continue;
                }

                if (advertisingManifestVersionAndWorkloads != null &&
                    advertisingManifestVersionAndWorkloads.Value.ManifestVersion.CompareTo(currentManifestVersion) > 0)
                {
                    manifestUpdates.Add((manifestId, currentManifestVersion,
                        advertisingManifestVersionAndWorkloads.Value.ManifestVersion,
                        advertisingManifestVersionAndWorkloads.Value.Workloads));
                }
            }

            return manifestUpdates;
        }

        public async Task<IEnumerable<string>> DownloadManifestPackagesAsync(bool includePreviews, DirectoryPath downloadPath)
        {
            var manifests = GetInstalledManifestIds();
            var packagePaths = new List<string>();
            foreach (var manifest in manifests)
            {
                try
                {
                    var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(
                        GetManifestPackageId(_sdkFeatureBand, manifest),
                        packageSourceLocation: _packageSourceLocation,
                        includePreview: includePreviews, downloadFolder: downloadPath);
                    packagePaths.Add(packagePath);
                }
                catch (Exception)
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.FailedToDownloadPackageManifest, manifest));
                }
            }
            return packagePaths;
        }

        public async Task ExtractManifestPackagesToTempDirAsync(IEnumerable<string> manifestPackages, DirectoryPath tempDir)
        {
            Directory.CreateDirectory(tempDir.Value);
            foreach (var manifestPackagePath in manifestPackages)
            {
                var manifestId = Path.GetFileNameWithoutExtension(manifestPackagePath);
                var extractionDir = Path.Combine(tempDir.Value, manifestId);
                Directory.CreateDirectory(extractionDir);
                await _nugetPackageDownloader.ExtractPackageAsync(manifestPackagePath, new DirectoryPath(extractionDir));
                File.Copy(Path.Combine(extractionDir, "data", "WorkloadManifest.json"), Path.Combine(tempDir.Value, manifestId, "WorkloadManifest.json"));
            }
        }

        public IEnumerable<string> GetManifestPackageUrls(bool includePreviews)
        {
            var packageIds = GetInstalledManifestIds()
                .Select(manifestId => GetManifestPackageId(_sdkFeatureBand, manifestId));

            var packageUrls = new List<string>();
            foreach (var packageId in packageIds)
            {
                try
                {
                    packageUrls.Add(_nugetPackageDownloader.GetPackageUrl(packageId, packageSourceLocation: _packageSourceLocation, includePreview: includePreviews).GetAwaiter().GetResult());
                }
                catch
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.FailedToGetPackageManifestUrl, packageId));
                }
            }
            return packageUrls;
        }

        private IEnumerable<ManifestId> GetInstalledManifestIds()
        {
            var manifestDirs = _workloadManifestProvider.GetManifestDirectories();

            var manifests = new List<ManifestId>();
            foreach (var manifestDir in manifestDirs)
            {
                var manifestId = Path.GetFileName(manifestDir);
                manifests.Add(new ManifestId(manifestId));
            }
            return manifests;
        }

        private async Task UpdateAdvertisingManifestAsync(ManifestId manifestId, bool includePreviews, DirectoryPath? offlineCache = null)
        {
            string packagePath = null;
            string extractionPath = null;

            try
            {
                var adManifestPath = GetAdvertisingManifestPath(_sdkFeatureBand, manifestId);
                if (offlineCache == null || !offlineCache.HasValue)
                {
                    try
                    {
                        packagePath = await _nugetPackageDownloader.DownloadPackageAsync(
                            GetManifestPackageId(_sdkFeatureBand, manifestId),
                            packageSourceLocation: _packageSourceLocation,
                            includePreview: includePreviews);
                    }
                    catch (NuGetPackageNotFoundException)
                    {
                        _reporter.WriteLine(string.Format(LocalizableStrings.AdManifestPackageDoesNotExist, manifestId));
                    }
                }
                else
                {
                    packagePath = Directory.GetFiles(offlineCache.Value.Value)
                        .Where(path => path.EndsWith(".nupkg"))
                        .Where(path => Path.GetFileName(path).StartsWith(GetManifestPackageId(_sdkFeatureBand, manifestId).ToString()))
                        .Max();
                    if (!File.Exists(packagePath))
                    {
                        throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, GetManifestPackageId(_sdkFeatureBand, manifestId), "*", offlineCache));
                    }
                }

                extractionPath = Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp", $"{manifestId}-extracted");
                Directory.CreateDirectory(extractionPath);
                var resultingFiles = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(extractionPath));

                if (Directory.Exists(adManifestPath))
                {
                    Directory.Delete(adManifestPath, true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(adManifestPath));
                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(Path.Combine(extractionPath, "data"), adManifestPath));

                _reporter.WriteLine(string.Format(LocalizableStrings.AdManifestUpdated, manifestId));

            }
            catch (Exception e)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.FailedAdManifestUpdate, manifestId, e.Message));
            }
            finally
            {
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, true);
                }

                if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath) && (offlineCache == null || !offlineCache.HasValue))
                {
                    File.Delete(packagePath);
                }

                var versionDir = Path.GetDirectoryName(packagePath);
                if (Directory.Exists(versionDir) && !Directory.GetFileSystemEntries(versionDir).Any())
                {
                    Directory.Delete(versionDir);
                    var idDir = Path.GetDirectoryName(versionDir);
                    if (Directory.Exists(idDir) && !Directory.GetFileSystemEntries(idDir).Any())
                    {
                        Directory.Delete(idDir);
                    }
                }
            }
        }

        private (ManifestVersion ManifestVersion, Dictionary<WorkloadId, WorkloadDefinition> Workloads)?
            GetAdvertisingManifestVersionAndWorkloads(ManifestId manifestId)
        {
            var manifestPath = Path.Combine(GetAdvertisingManifestPath(_sdkFeatureBand, manifestId),
                "WorkloadManifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using (FileStream fsSource = new FileStream(manifestPath, FileMode.Open, FileAccess.Read))
            {
                var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId.ToString(), fsSource);
                return (new ManifestVersion(manifest.Version), manifest.Workloads);
            }
        }

        private ManifestVersion GetInstalledManifestVersion(ManifestId manifestId)
        {
            var manifestDir = _workloadManifestProvider.GetManifestDirectories()
                .FirstOrDefault(dir => Path.GetFileName(dir).ToLowerInvariant().Equals(manifestId.ToString()));
            if (manifestDir == null)
            {
                throw new Exception(string.Format(LocalizableStrings.ManifestDoesNotExist, manifestId.ToString()));
            }

            var manifestPath = Path.Combine(manifestDir, "WorkloadManifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new Exception(string.Format(LocalizableStrings.ManifestDoesNotExist, manifestId.ToString()));
            }

            using (FileStream fsSource = new FileStream(manifestPath, FileMode.Open, FileAccess.Read))
            {
                var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId.ToString(), fsSource);
                return new ManifestVersion(manifest.Version);
            }
        }

        private bool AdManifestSentinalIsDueForUpdate()
        {
            var sentinalPath = GetAdvertisingManifestSentinalPath();
            int updateIntervalHours;
            if (!int.TryParse(_getEnvironmentVariable("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS"), out updateIntervalHours))
            {
                updateIntervalHours = 24;
            }

            if (File.Exists(sentinalPath))
            {
                var lastAccessTime = File.GetLastAccessTime(sentinalPath);
                if (lastAccessTime.AddHours(updateIntervalHours) > DateTime.Now)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> UpdatedAdManifestPackagesExistAsync()
        {
            var manifests = GetInstalledManifestIds();
            var avaliableUpdates = await Task.WhenAll(manifests.Select(manifest => NewerManifestPackageExists(manifest)))
                .ConfigureAwait(false);
            return avaliableUpdates.Any();
        }

        private async Task<bool> NewerManifestPackageExists(ManifestId manifest)
        {
            try
            {
                var currentVersion = NuGetVersion.Parse(_workloadResolver.GetManifestVersion(manifest.ToString()));
                var latestVersion = await _nugetPackageDownloader.GetLatestPackageVerion(GetManifestPackageId(_sdkFeatureBand, manifest));
                return latestVersion > currentVersion;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool BackgroundUpdatesAreDisabled() =>
            bool.TryParse(_getEnvironmentVariable("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"), out var disableEnvVar) && disableEnvVar;

        private string GetAdvertisingManifestSentinalPath() => Path.Combine(_userHome, ".dotnet", ".workloadAdvertisingManifestSentinal");

        private string GetAdvertisingManifestPath(SdkFeatureBand featureBand, ManifestId manifestId) =>
            Path.Combine(_userHome, ".dotnet", "sdk-advertising", featureBand.ToString(), manifestId.ToString());

        internal static PackageId GetManifestPackageId(SdkFeatureBand featureBand, ManifestId manifestId) =>
            new PackageId($"{manifestId}.Manifest-{featureBand}");
    }
}
