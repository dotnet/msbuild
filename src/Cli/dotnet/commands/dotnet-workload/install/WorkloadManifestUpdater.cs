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

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        private readonly IReporter _reporter;
        private readonly IWorkloadManifestProvider _workloadManifestProvider;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly string _userHome;

        public WorkloadManifestUpdater(
            IReporter reporter,
            IWorkloadManifestProvider workloadManifestProvider,
            INuGetPackageDownloader nugetPackageDownloader,
            string userHome)
        {
            _reporter = reporter;
            _workloadManifestProvider = workloadManifestProvider;
            _userHome = userHome;
            _nugetPackageDownloader = nugetPackageDownloader;
        }

        public async Task UpdateAdvertisingManifestsAsync(SdkFeatureBand featureBand)
        {
            var manifests = GetInstalledManifestIds();
            foreach (var manifest in manifests)
            {
                await UpdateAdvertisingManifestAsync(manifest, featureBand);
            }
        }

        public IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> CalculateManifestUpdates(SdkFeatureBand featureBand)
        {
            var manifestUpdates = new List<(ManifestId, ManifestVersion, ManifestVersion)>();
            var currentManifestIds = GetInstalledManifestIds();
            foreach (var manifestId in currentManifestIds)
            {
                var currentManifestVersion = GetInstalledManifestVersion(manifestId);
                var adManifestVersion = GetAdvertisingManifestVersion(featureBand, manifestId);
                if (adManifestVersion == null)
                {
                    continue;
                }

                if (adManifestVersion != null && adManifestVersion.CompareTo(currentManifestVersion) > 0)
                {
                    manifestUpdates.Add((manifestId, currentManifestVersion, adManifestVersion));
                }
            }
            return manifestUpdates;
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

        private async Task UpdateAdvertisingManifestAsync(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            string packagePath = null;
            try
            {
                var adManifestPath = GetAdvertisingManifestPath(featureBand, manifestId);
                packagePath = await _nugetPackageDownloader.DownloadPackageAsync(GetManifestPackageId(featureBand, manifestId));
                var resultingFiles = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, adManifestPath);
                _reporter.WriteLine(string.Format(LocalizableStrings.AdManifestUpdated, manifestId));
            }
            catch (Exception e)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.FailedAdManifestUpdate, manifestId, e.Message));
            }
            finally
            {
                if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath))
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

        private ManifestVersion GetAdvertisingManifestVersion(SdkFeatureBand featureBand, ManifestId manifestId)
        {
            var manifestPath = Path.Combine(GetAdvertisingManifestPath(featureBand, manifestId), "WorkloadManifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using (FileStream fsSource = new FileStream(manifestPath, FileMode.Open, FileAccess.Read))
            {
                var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId.ToString(), fsSource);
                return new ManifestVersion(manifest.Version);
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

        private string GetAdvertisingManifestPath(SdkFeatureBand featureBand, ManifestId manifestId) =>
            Path.Combine(_userHome, ".dotnet", "sdk-advertising", featureBand.ToString(), manifestId.ToString());

        internal static PackageId GetManifestPackageId(SdkFeatureBand featureBand, ManifestId manifestId) =>
            new PackageId($"{manifestId}.Manifest-{featureBand}");
    }
}
