// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageInstaller;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NetSdkManagedInstaller : PackWorkloadInstallerBase
    {
        private readonly IReporter _reporter;
        private readonly string _workloadMetadataDir;
        private readonly string _installedWorkloadDir = "InstalledWorkloads";
        private readonly string _installedPacksDir = "InstalledPacks";
        protected readonly string _dotnetDir;
        protected readonly string _tempPackagesDir;
        private readonly INuGetPackageInstaller _nugetPackageInstaller;

        public NetSdkManagedInstaller(
            IReporter reporter,
            INuGetPackageInstaller nugetPackageInstaller = null,
            string dotnetDir =  null)
        {
            _dotnetDir = dotnetDir ?? EnvironmentProvider.GetDotnetExeDirectory();
            _tempPackagesDir = Path.Combine(_dotnetDir, "metadata", "temp");
            _nugetPackageInstaller = nugetPackageInstaller ?? new NuGetPackageInstaller(_tempPackagesDir, sourceUrl: "https://pkgs.dev.azure.com/azure-public/vside/_packaging/xamarin-impl/nuget/v3/index.json");
            _workloadMetadataDir = Path.Combine(_dotnetDir, "metadata", "workloads");
            _reporter = reporter;
        }

        public override void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false)
        {
            if (useOfflineCache)
            {
                throw new NotImplementedException();
            }

            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingPackVersionMessage, packInfo.Id, packInfo.Version));
            var tempsToDelete = new List<string>();
            try
            {
                TransactionalAction.Run(
                    action: () =>
                    {
                        if (!PackIsInstalled(packInfo))
                        {
                            var packagePath = _nugetPackageInstaller.InstallPackageAsync(new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)).Result;
                            tempsToDelete.Add(packagePath);

                            if (!Directory.Exists(Path.GetDirectoryName(packInfo.Path)))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(packInfo.Path));
                            }

                            if (isSingleFilePack(packInfo))
                            {
                                File.Copy(packagePath, packInfo.Path);
                            }
                            else
                            {
                                var tempExtractionDir = Path.Combine(_tempPackagesDir, $"{packInfo.Id}-extracted");
                                tempsToDelete.Add(tempExtractionDir);
                                Directory.CreateDirectory(tempExtractionDir);
                                var packFiles = _nugetPackageInstaller.ExtractPackageAsync(packagePath, tempExtractionDir).Result;

                                FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(tempExtractionDir, packInfo.Path));
                            }
                        }
                        else
                        {
                            _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadPackAlreadyInstalledMessage, packInfo.Id, packInfo.Version));
                        }

                        WritePackInstallationRecord(packInfo, sdkFeatureBand);
                    },
                    rollback: () => {
                        RollBackWorkloadPackInstall(packInfo, sdkFeatureBand);
                    });
            }
            finally
            {
                // Delete leftover nupkgs that have been extracted
                foreach (var file in tempsToDelete)
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        var packagePath = Path.GetDirectoryName(file);
                        while (!Directory.EnumerateFileSystemEntries(packagePath).Any())
                        {
                            Directory.Delete(packagePath);
                            packagePath = Path.GetDirectoryName(packagePath);
                        }
                    }
                }
            }
        }

        public override void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            DeletePackInstallationRecord(packInfo, sdkFeatureBand);
            if (!PackHasInstallRecords(packInfo))
            {
                DeletePack(packInfo);
            }
        }

        public override void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand) => throw new NotImplementedException();

        public override void DownloadToOfflineCache(IReadOnlyCollection<string> manifests) => throw new NotImplementedException();

        public override void GarbageCollectInstalledWorkloadPacks()
        {
            var sdkFeatureBands = GetFeatureBandsWithInstallationRecords();
            _reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectingSdkFeatureBandsMessage, string.Join(", ", sdkFeatureBands)));
            var deletablePacks = GetDeletablePacks(sdkFeatureBands);

            foreach (var (deletablePack, featureBand) in deletablePacks)
            {
                DeletePackInstallationRecord(deletablePack, featureBand);
                if (!PackHasInstallRecords(deletablePack))
                {
                    DeletePack(deletablePack);
                }
            }
        }

        private IEnumerable<(PackInfo, SdkFeatureBand)> GetDeletablePacks(IEnumerable<SdkFeatureBand> sdkFeatureBands)
        {
            var installedPacksDir = Path.Combine(_workloadMetadataDir, _installedPacksDir);
            IEnumerable<(PackInfo, SdkFeatureBand)> deletablePacks = new List<(PackInfo, SdkFeatureBand)>();
            foreach (var featureBand in sdkFeatureBands)
            {
                var workloadResolver = GetWorkloadResolver(featureBand);
                var installedWorkloads = GetInstalledWorkloads(featureBand);

                var expectedPacks = installedWorkloads
                    .SelectMany(workload => workloadResolver.GetPacksInWorkload(workload))
                    .Select(pack => workloadResolver.TryGetPackInfo(pack))
                    .Select(packInfo => GetPackInstallRecordPath(packInfo, featureBand));

                var currentFeatureBandPackRecords = Directory.EnumerateDirectories(installedPacksDir)
                    .SelectMany(packIdDir => Directory.EnumerateDirectories(packIdDir))
                    .SelectMany(packVersionDir => Directory.EnumerateFiles(packVersionDir));

                currentFeatureBandPackRecords = currentFeatureBandPackRecords
                    .Where(featureBandDir => Path.GetFileName(featureBandDir).Equals(featureBand.ToString()));

                var featureBandPacksToDelete = currentFeatureBandPackRecords.Except(expectedPacks)
                    .Select(packRecordPath => (GetPackInfo(workloadResolver, packRecordPath), featureBand));

                deletablePacks = deletablePacks.Concat(featureBandPacksToDelete);
            }
            return deletablePacks;
        }

        private PackInfo GetPackInfo(WorkloadResolver workloadResolver, string packRecordPath)
        {
            var versionRecordPath = Path.GetDirectoryName(packRecordPath);
            var idRecordPath = Path.GetDirectoryName(versionRecordPath);
            var packId = Path.GetFileName(idRecordPath);
            return workloadResolver.TryGetPackInfo(packId);
        }

        protected virtual WorkloadResolver GetWorkloadResolver(SdkFeatureBand featureBand)
        {
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetDir, featureBand.ToString());
            return WorkloadResolver.Create(workloadManifestProvider, _dotnetDir, featureBand.ToString());
        }

        public override IReadOnlyCollection<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            if (Directory.Exists(_workloadMetadataDir))
            {
                var bands = Directory.EnumerateDirectories(_workloadMetadataDir);
                return bands
                    .Where(band => Directory.Exists(Path.Combine(band, _installedWorkloadDir)))
                    .Select(path => new SdkFeatureBand(Path.GetFileName(path)))
                    .ToList().AsReadOnly();
            }
            else
            {
                return new List<SdkFeatureBand>().AsReadOnly();
            }
        }

        public override IReadOnlyCollection<string> GetInstalledWorkloads(SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir);
            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path)
                    .Select(file => Path.GetFileName(file))
                    .ToList().AsReadOnly();
            }
            else
            {
                return new List<string>().AsReadOnly();
            }
        }

        public override void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingWorkloadInstallRecordMessage, workloadId));
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir, workloadId.ToString());
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, string.Empty);
        }

        public override void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand featureBand)
        {
            var path = Path.Combine(_workloadMetadataDir, featureBand.ToString(), _installedWorkloadDir, workloadId.ToString());
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private bool PackIsInstalled(PackInfo packInfo)
        {
            if (isSingleFilePack(packInfo))
            {
                return File.Exists(packInfo.Path);
            }
            else
            {
                return Directory.Exists(packInfo.Path);
            }
        }

        private void DeletePack(PackInfo packInfo)
        {
            if (PackIsInstalled(packInfo))
            {
                if (isSingleFilePack(packInfo))
                {
                    File.Delete(packInfo.Path);
                }
                else
                {
                    Directory.Delete(packInfo.Path, true);
                    var packIdDir = Path.GetDirectoryName(packInfo.Path);
                    if (!Directory.EnumerateFileSystemEntries(packIdDir).Any())
                    {
                        Directory.Delete(packIdDir, true);
                    }
                }
            }
        }

        private string GetPackInstallRecordPath(PackInfo packInfo, SdkFeatureBand featureBand) =>
            Path.Combine(_workloadMetadataDir, _installedPacksDir, packInfo.Id, packInfo.Version, featureBand.ToString());

        private void WritePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingPackInstallRecordMessage, packInfo.Id, packInfo.Version));
            var path = GetPackInstallRecordPath(packInfo, featureBand);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, string.Empty);
        }

        private void DeletePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            var packInstallRecord = GetPackInstallRecordPath(packInfo, featureBand);
            if (File.Exists(packInstallRecord))
            {
                File.Delete(packInstallRecord);

                var packRecordVersionDir = Path.GetDirectoryName(packInstallRecord);
                if (!Directory.EnumerateFileSystemEntries(packRecordVersionDir).Any())
                {
                    Directory.Delete(packRecordVersionDir);

                    var packRecordIdDir = Path.GetDirectoryName(packRecordVersionDir);
                    if (!Directory.EnumerateFileSystemEntries(packRecordIdDir).Any())
                    {
                        Directory.Delete(packRecordIdDir);
                    }
                }
            }
        }

        private bool PackHasInstallRecords(PackInfo packInfo)
        {
            var packInstallRecordDir = Path.Combine(_workloadMetadataDir, _installedPacksDir, packInfo.Id, packInfo.Version);
            return Directory.Exists(packInstallRecordDir) && Directory.EnumerateFileSystemEntries(packInstallRecordDir).Any();
        }

        private bool isSingleFilePack(PackInfo packInfo) => packInfo.Kind.Equals(WorkloadPackKind.Library) || packInfo.Kind.Equals(WorkloadPackKind.Template);
    }
}
