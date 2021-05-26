// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using System.Text.Json;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NetSdkManagedInstaller : IWorkloadPackInstaller
    {
        private readonly IReporter _reporter;
        private readonly string _workloadMetadataDir;
        private readonly string _installedPacksDir = "InstalledPacks";
        protected readonly string _dotnetDir;
        protected readonly DirectoryPath _tempPackagesDir;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly NetSdkManagedInstallationRecordRepository _installationRecordRepository;
        private readonly PackageSourceLocation _packageSourceLocation;

        public NetSdkManagedInstaller(
            IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir =  null,
            VerbosityOptions verbosity = VerbosityOptions.normal, 
            PackageSourceLocation packageSourceLocation = null)
        {
            _dotnetDir = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _tempPackagesDir = new DirectoryPath(Path.Combine(_dotnetDir, "metadata", "temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? 
                new NuGetPackageDownloader(_tempPackagesDir, filePermissionSetter: null, verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger());
            _workloadMetadataDir = Path.Combine(_dotnetDir, "metadata", "workloads");
            _reporter = reporter;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _installationRecordRepository = new NetSdkManagedInstallationRecordRepository(_dotnetDir);
            _packageSourceLocation = packageSourceLocation;
        }

        public InstallationUnit GetInstallationUnit()
        {
            return InstallationUnit.Packs;
        }

        public IWorkloadPackInstaller GetPackInstaller()
        {
            return this;
        }

        public IWorkloadInstaller GetWorkloadInstaller()
        {
            throw new Exception("NetSdkManagedInstaller is not a workload installer.");
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository()
        {
            return _installationRecordRepository;
        }

        public void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingPackVersionMessage, packInfo.Id, packInfo.Version));
            var tempDirsToDelete = new List<string>();
            var tempFilesToDelete = new List<string>();
            try
            {
                TransactionalAction.Run(
                    action: () =>
                    {
                        if (!PackIsInstalled(packInfo))
                        {
                            string packagePath;
                            if (offlineCache == null || !offlineCache.HasValue)
                            {
                                packagePath = _nugetPackageDownloader.DownloadPackageAsync(new PackageId(packInfo.ResolvedPackageId), new NuGetVersion(packInfo.Version), _packageSourceLocation).Result;
                                tempFilesToDelete.Add(packagePath);
                            }
                            else
                            {
                                _reporter.WriteLine(string.Format(LocalizableStrings.UsingCacheForPackInstall, packInfo.Id, packInfo.Version, offlineCache));
                                packagePath = Path.Combine(offlineCache.Value.Value, $"{packInfo.ResolvedPackageId}.{packInfo.Version}.nupkg");
                                if (!File.Exists(packagePath))
                                {
                                    throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, packInfo.ResolvedPackageId, packInfo.Version, offlineCache));
                                }
                            }

                            if (!Directory.Exists(Path.GetDirectoryName(packInfo.Path)))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(packInfo.Path));
                            }

                            if (IsSingleFilePack(packInfo))
                            {
                                File.Copy(packagePath, packInfo.Path);
                            }
                            else
                            {
                                var tempExtractionDir = Path.Combine(_tempPackagesDir.Value, $"{packInfo.Id}-{packInfo.Version}-extracted");
                                tempDirsToDelete.Add(tempExtractionDir);
                                Directory.CreateDirectory(tempExtractionDir);
                                var packFiles = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempExtractionDir)).Result;

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
                        try
                        {
                            _reporter.WriteLine(string.Format(LocalizableStrings.RollingBackPackInstall, packInfo.Id));
                            RollBackWorkloadPackInstall(packInfo, sdkFeatureBand);
                        }
                        catch (Exception e)
                        {
                            // Don't hide the original error if roll back fails
                            _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, e.Message));
                        }
                    });
            }
            finally
            {
                // Delete leftover dirs and files
                foreach (var file in tempFilesToDelete)
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                foreach (var dir in tempDirsToDelete)
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
        }

        public void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            DeletePackInstallationRecord(packInfo, sdkFeatureBand);
            if (!PackHasInstallRecords(packInfo))
            {
                DeletePack(packInfo);
            }
        }

        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            string packagePath = null;
            string tempExtractionDir = null;
            string tempBackupDir = null;
            var manifestPath = Path.Combine(_dotnetDir, "sdk-manifests", sdkFeatureBand.ToString(), manifestId.ToString());

            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingWorkloadManifest, manifestId, manifestVersion));

            try
            {
                TransactionalAction.Run(
                   action: () =>
                   {
                       if (offlineCache == null || !offlineCache.HasValue)
                       {
                           packagePath = _nugetPackageDownloader.DownloadPackageAsync(WorkloadManifestUpdater.GetManifestPackageId(sdkFeatureBand, manifestId), new NuGetVersion(manifestVersion.ToString()), _packageSourceLocation).Result;
                       }
                       else
                       {
                           packagePath = Path.Combine(offlineCache.Value.Value, $"{WorkloadManifestUpdater.GetManifestPackageId(sdkFeatureBand, manifestId)}.{manifestVersion}.nupkg");
                           if (!File.Exists(packagePath))
                           {
                               throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, WorkloadManifestUpdater.GetManifestPackageId(sdkFeatureBand, manifestId), manifestVersion, offlineCache));
                           }
                       }
                       tempExtractionDir = Path.Combine(_tempPackagesDir.Value, $"{manifestId}-{manifestVersion}-extracted");
                       Directory.CreateDirectory(tempExtractionDir);
                       var manifestFiles = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempExtractionDir)).Result;

                       if (Directory.Exists(manifestPath) && Directory.GetFileSystemEntries(manifestPath).Any())
                       {
                           // Backup existing manifest data for roll back purposes
                           tempBackupDir = Path.Combine(_tempPackagesDir.Value, $"{manifestId}-{manifestVersion}-backup");
                           if (Directory.Exists(tempBackupDir))
                           {
                               Directory.Delete(tempBackupDir, true);
                           }
                           FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(manifestPath, tempBackupDir));
                       }
                       Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                       FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(Path.Combine(tempExtractionDir, "data"), manifestPath));
                   },
                    rollback: () =>
                    {
                        if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                        {
                            FileAccessRetrier.RetryOnMoveAccessFailure(() => Directory.Move(tempBackupDir, manifestPath));
                        }
                    });

                // Delete leftover dirs and files
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

                if (!string.IsNullOrEmpty(tempExtractionDir) && Directory.Exists(tempExtractionDir))
                {
                    Directory.Delete(tempExtractionDir, true);
                }

                if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                {
                    Directory.Delete(tempBackupDir, true);
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format(LocalizableStrings.FailedToInstallWorkloadManifest, manifestId, manifestVersion, e.Message));
            }
        }

        public void DownloadToOfflineCache(PackInfo packInfo, DirectoryPath cachePath, bool includePreviews)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.DownloadingPackToCacheMessage, packInfo.Id, packInfo.Version, cachePath.Value));
            if (!Directory.Exists(cachePath.Value))
            {
                Directory.CreateDirectory(cachePath.Value);
            }
            _nugetPackageDownloader.DownloadPackageAsync(new PackageId(packInfo.ResolvedPackageId), new NuGetVersion(packInfo.Version), downloadFolder: cachePath,
                packageSourceLocation: _packageSourceLocation, includePreview: includePreviews).Wait();
        }

        public void GarbageCollectInstalledWorkloadPacks()
        {
            var installedPacksDir = Path.Combine(_workloadMetadataDir, _installedPacksDir, "v1");
            var installedSdkFeatureBands = _installationRecordRepository.GetFeatureBandsWithInstallationRecords();
            _reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectingSdkFeatureBandsMessage, string.Join(" ", installedSdkFeatureBands)));
            var currentBandInstallRecords = GetExpectedPackInstallRecords(_sdkFeatureBand);

            foreach (var packIdDir in Directory.GetDirectories(installedPacksDir))
            {
                foreach (var packVersionDir in Directory.GetDirectories(packIdDir))
                {
                    var bandRecords = Directory.GetFileSystemEntries(packVersionDir);

                    var unneededBandRecords = bandRecords
                        .Where(recordPath => !installedSdkFeatureBands.Contains(new SdkFeatureBand(Path.GetFileName(recordPath))));

                    var currentBandRecordPath = Path.Combine(packVersionDir, _sdkFeatureBand.ToString());
                    if (bandRecords.Contains(currentBandRecordPath) && !currentBandInstallRecords.Contains(currentBandRecordPath))
                    {
                        unneededBandRecords = unneededBandRecords.Append(currentBandRecordPath);
                    }

                    if (!unneededBandRecords.Any())
                    {
                        continue;
                    }

                    // Save the pack info in case we need to delete the pack
                    var jsonPackInfo = File.ReadAllText(unneededBandRecords.First());
                    foreach (var unneededRecord in unneededBandRecords)
                    {
                        File.Delete(unneededRecord);
                    }

                    if (!bandRecords.Except(unneededBandRecords).Any())
                    {
                        Directory.Delete(packVersionDir);
                        var deletablePack = GetPackInfo(packVersionDir);
                        if (deletablePack == null)
                        {
                            // Pack no longer exists in manifests, get pack info from installation record
                            deletablePack = JsonSerializer.Deserialize(jsonPackInfo, typeof(PackInfo)) as PackInfo;
                        }
                        DeletePack(deletablePack);
                    }
                }

                if (!Directory.GetFileSystemEntries(packIdDir).Any())
                {
                    Directory.Delete(packIdDir);
                }
            }
        }

        public IEnumerable<PackInfo> GetInstalledPacks(SdkFeatureBand sdkFeatureBand)
        {
            var installedPacksDir = Path.Combine(_workloadMetadataDir, _installedPacksDir, "v1");
            return Directory.GetDirectories(installedPacksDir)
                .Select(packIdPath => Path.GetFileName(packIdPath))
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);
        }

        private IEnumerable<string> GetExpectedPackInstallRecords(SdkFeatureBand sdkFeatureBand)
        {
            var installedWorkloads = _installationRecordRepository.GetInstalledWorkloads(sdkFeatureBand);
            return installedWorkloads
                .SelectMany(workload => _workloadResolver.GetPacksInWorkload(workload.ToString()))
                .Select(pack => _workloadResolver.TryGetPackInfo(pack))
                .Where(pack => pack != null)
                .Select(packInfo => GetPackInstallRecordPath(packInfo, sdkFeatureBand));
        }

        private PackInfo GetPackInfo(string packRecordDir)
        {
            // Expected path: <DOTNET ROOT>/metadata/workloads/installedpacks/v1/<Pack ID>/<Pack Version>/
            var idRecordPath = Path.GetDirectoryName(packRecordDir);
            var packId = Path.GetFileName(idRecordPath);
            var packInfo = _workloadResolver.TryGetPackInfo(packId);
            if (packInfo != null && packInfo.Version.Equals(Path.GetFileName(packRecordDir)))
            {
                return packInfo;
            }
            return null;
        }

        private bool PackIsInstalled(PackInfo packInfo)
        {
            if (IsSingleFilePack(packInfo))
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
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (IsSingleFilePack(packInfo))
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
            Path.Combine(_workloadMetadataDir, _installedPacksDir, "v1", packInfo.Id, packInfo.Version, featureBand.ToString());

        private void WritePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingPackInstallRecordMessage, packInfo.Id, packInfo.Version));
            var path = GetPackInstallRecordPath(packInfo, featureBand);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, JsonSerializer.Serialize(packInfo));
        }

        private void DeletePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            var packInstallRecord = GetPackInstallRecordPath(packInfo, featureBand);
            if (File.Exists(packInstallRecord))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(packInstallRecord);

                var packRecordVersionDir = Path.GetDirectoryName(packInstallRecord);
                if (!Directory.GetFileSystemEntries(packRecordVersionDir).Any())
                {
                    Directory.Delete(packRecordVersionDir);

                    var packRecordIdDir = Path.GetDirectoryName(packRecordVersionDir);
                    if (!Directory.GetFileSystemEntries(packRecordIdDir).Any())
                    {
                        Directory.Delete(packRecordIdDir);
                    }
                }
            }
        }

        private bool PackHasInstallRecords(PackInfo packInfo)
        {
            var packInstallRecordDir = Path.Combine(_workloadMetadataDir, _installedPacksDir, "v1", packInfo.Id, packInfo.Version);
            return Directory.Exists(packInstallRecordDir) && Directory.GetFiles(packInstallRecordDir).Any();
        }

        private bool IsSingleFilePack(PackInfo packInfo) => packInfo.Kind.Equals(WorkloadPackKind.Library) || packInfo.Kind.Equals(WorkloadPackKind.Template);
    }
}
