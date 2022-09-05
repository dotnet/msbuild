// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class FileBasedInstaller : IInstaller
    {
        private readonly IReporter _reporter;
        private readonly string _workloadMetadataDir;
        private const string InstalledPacksDir = "InstalledPacks";
        protected readonly string _dotnetDir;
        protected readonly string _userProfileDir;
        protected readonly DirectoryPath _tempPackagesDir;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private IWorkloadResolver _workloadResolver;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly FileBasedInstallationRecordRepository _installationRecordRepository;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly RestoreActionConfig _restoreActionConfig;

        public int ExitCode => 0;

        public FileBasedInstaller(IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            string userProfileDir,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            RestoreActionConfig restoreActionConfig = null)
        {
            _userProfileDir = userProfileDir;
            _dotnetDir = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _tempPackagesDir = new DirectoryPath(tempDirPath ?? Path.GetTempPath());
            ILogger logger = verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger();
            _restoreActionConfig = restoreActionConfig;
            _nugetPackageDownloader = nugetPackageDownloader ??
                                      new NuGetPackageDownloader(_tempPackagesDir, filePermissionSetter: null,
                                          new FirstPartyNuGetPackageSigningVerifier(), logger,
                                          restoreActionConfig: _restoreActionConfig);
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(_dotnetDir, sdkFeatureBand.ToString());
            _workloadMetadataDir = Path.Combine(userLocal ? _userProfileDir : _dotnetDir, "metadata", "workloads");
            _reporter = reporter;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _installationRecordRepository = new FileBasedInstallationRecordRepository(_workloadMetadataDir);
            _packageSourceLocation = packageSourceLocation;
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository()
        {
            return _installationRecordRepository;
        }

        public void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver)
        {
            _workloadResolver = workloadResolver;
        }

        IEnumerable<PackInfo> GetPacksInWorkloads(IEnumerable<WorkloadId> workloadIds)
        {
            var packs = workloadIds
                .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);

            return packs;
        }


        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            var packInfos = GetPacksInWorkloads(workloadIds);

            foreach (var packInfo in packInfos)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.InstallingPackVersionMessage, packInfo.ResolvedPackageId, packInfo.Version));
                var tempDirsToDelete = new List<string>();
                var tempFilesToDelete = new List<string>();
                bool shouldRollBackPack = false;

                transactionContext.Run(
                    action: () =>
                    {
                        if (!PackIsInstalled(packInfo))
                        {
                            shouldRollBackPack = true;
                            string packagePath;
                            if (offlineCache == null || !offlineCache.HasValue)
                            {
                                packagePath = _nugetPackageDownloader
                                    .DownloadPackageAsync(new PackageId(packInfo.ResolvedPackageId),
                                        new NuGetVersion(packInfo.Version),
                                        _packageSourceLocation).GetAwaiter().GetResult();
                                tempFilesToDelete.Add(packagePath);
                            }
                            else
                            {
                                _reporter.WriteLine(string.Format(LocalizableStrings.UsingCacheForPackInstall, packInfo.ResolvedPackageId, packInfo.Version, offlineCache));
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
                                var tempExtractionDir = Path.Combine(_tempPackagesDir.Value, $"{packInfo.ResolvedPackageId}-{packInfo.Version}-extracted");
                                tempDirsToDelete.Add(tempExtractionDir);
                                Directory.CreateDirectory(tempExtractionDir);
                                var packFiles = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempExtractionDir)).GetAwaiter().GetResult();

                                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(tempExtractionDir, packInfo.Path));
                            }
                        }
                        else
                        {
                            _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadPackAlreadyInstalledMessage, packInfo.ResolvedPackageId, packInfo.Version));
                        }

                        WritePackInstallationRecord(packInfo, sdkFeatureBand);
                    },
                    rollback: () =>
                    {
                        try
                        {
                            if (shouldRollBackPack)
                            {
                                _reporter.WriteLine(string.Format(LocalizableStrings.RollingBackPackInstall, packInfo.ResolvedPackageId));
                                DeletePackInstallationRecord(packInfo, sdkFeatureBand);
                                if (!PackHasInstallRecords(packInfo))
                                {
                                    DeletePack(packInfo);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't hide the original error if roll back fails
                            _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, e.Message));
                        }
                    },
                    cleanup: () =>
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
                    });         
            }
        }

        public void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            //  TODO: Actually re-extract the packs to fix any corrupted files. 
            CliTransaction.RunNew(context => InstallWorkloads(workloadIds, sdkFeatureBand, context, offlineCache));
        }

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null, bool isRollback = false)
        {
            string packagePath = null;
            string tempBackupDir = null;
            string rootInstallDir = WorkloadFileBasedInstall.IsUserLocal(_dotnetDir, _sdkFeatureBand.ToString()) ? _userProfileDir : _dotnetDir;
            var newManifestPath = Path.Combine(rootInstallDir, "sdk-manifests", manifestUpdate.NewFeatureBand, manifestUpdate.ManifestId.ToString());

            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingWorkloadManifest, manifestUpdate.ManifestId, manifestUpdate.NewVersion));

            try
            {
                transactionContext.Run(
                    action: () =>
                    {
                        var newManifestPackageId = GetManifestPackageId(manifestUpdate.ManifestId, new SdkFeatureBand(manifestUpdate.NewFeatureBand));
                        if (offlineCache == null || !offlineCache.HasValue)
                        {
                            packagePath = _nugetPackageDownloader.DownloadPackageAsync(newManifestPackageId,
                                new NuGetVersion(manifestUpdate.NewVersion.ToString()), _packageSourceLocation).GetAwaiter().GetResult();
                        }
                        else
                        {
                            packagePath = Path.Combine(offlineCache.Value.Value, $"{newManifestPackageId}.{manifestUpdate.NewVersion}.nupkg");
                            if (!File.Exists(packagePath))
                            {
                                throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, newManifestPackageId, manifestUpdate.NewVersion, offlineCache));
                            }
                        }

                        if (Directory.Exists(newManifestPath) && Directory.GetFileSystemEntries(newManifestPath).Any())
                        {
                            // Backup existing manifest data for roll back purposes
                            tempBackupDir = Path.Combine(_tempPackagesDir.Value, $"{manifestUpdate.ManifestId}-{manifestUpdate.ExistingVersion}-backup");
                            if (Directory.Exists(tempBackupDir))
                            {
                                Directory.Delete(tempBackupDir, true);
                            }
                            FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(newManifestPath, tempBackupDir));
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(newManifestPath));

                        ExtractManifestAsync(packagePath, newManifestPath).GetAwaiter().GetResult();
                    },
                    rollback: () =>
                    {
                        if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                        {
                            FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(tempBackupDir, newManifestPath));
                        }
                    },
                    cleanup: () =>
                    {
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

                        if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                        {
                            Directory.Delete(tempBackupDir, true);
                        }
                    });
            }
            catch (Exception e)
            {
                throw new Exception(string.Format(LocalizableStrings.FailedToInstallWorkloadManifest, manifestUpdate.ManifestId, manifestUpdate.NewVersion, e.Message), e);
            }
        }

        public IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems)
        {
            var packs = GetPacksInWorkloads(workloadIds);
            if (!includeInstalledItems)
            {
                packs = packs.Where(p => !PackIsInstalled(p));
            }

            return packs.Select(p => new WorkloadDownload(p.Id, p.ResolvedPackageId, p.Version)).ToList();
        }

        public void GarbageCollectInstalledWorkloadPacks(DirectoryPath? offlineCache = null)
        {
            var installedSdkFeatureBands = _installationRecordRepository.GetFeatureBandsWithInstallationRecords();
            _reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectingSdkFeatureBandsMessage, string.Join(" ", installedSdkFeatureBands)));
            var currentBandInstallRecords = GetExpectedPackInstallRecords(_sdkFeatureBand);
            string installedPacksDir = Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1");

            if (!Directory.Exists(installedPacksDir))
            {
                return;
            }

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

        public void Shutdown()
        {
            // Perform any additional cleanup here that's intended to run at the end of the command, regardless
            // of success or failure. For file based installs, there shouldn't be any additional work to 
            // perform.
        }

        public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            return new PackageId($"{manifestId}.Manifest-{featureBand}");
        }

        public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
        {
            var extractionPath = Path.Combine(_tempPackagesDir.Value, "dotnet-sdk-advertising-temp", $"{Path.GetFileName(nupkgPath)}-extracted");
            if (Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, true);
            }

            try
            {
                Directory.CreateDirectory(extractionPath);
                await _nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(Path.Combine(extractionPath, "data"), targetPath));
            }
            finally
            {
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, true);
                }
            }
        }

        private IEnumerable<string> GetExpectedPackInstallRecords(SdkFeatureBand sdkFeatureBand)
        {
            var installedWorkloads = _installationRecordRepository.GetInstalledWorkloads(sdkFeatureBand);
            return installedWorkloads
                .SelectMany(workload => _workloadResolver.GetPacksInWorkload(workload))
                .Select(pack => _workloadResolver.TryGetPackInfo(pack))
                .Where(pack => pack != null)
                .Select(packInfo => GetPackInstallRecordPath(packInfo, sdkFeatureBand));
        }

        private PackInfo GetPackInfo(string packRecordDir)
        {
            // Expected path: <DOTNET ROOT>/metadata/workloads/installedpacks/v1/<Pack ID>/<Pack Version>/
            var idRecordPath = Path.GetDirectoryName(packRecordDir);
            var packId = Path.GetFileName(idRecordPath);
            var packInfo = _workloadResolver.TryGetPackInfo(new WorkloadPackId(packId));
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
                _reporter.WriteLine(string.Format(LocalizableStrings.DeletingWorkloadPack, packInfo.Id, packInfo.Version));
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
            Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1", packInfo.Id, packInfo.Version, featureBand.ToString());

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
            var packInstallRecordDir = Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1", packInfo.Id, packInfo.Version);
            return Directory.Exists(packInstallRecordDir) && Directory.GetFiles(packInstallRecordDir).Any();
        }

        private bool IsSingleFilePack(PackInfo packInfo) => packInfo.Kind.Equals(WorkloadPackKind.Library) || packInfo.Kind.Equals(WorkloadPackKind.Template);
    }
}
