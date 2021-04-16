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
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NetSdkManagedInstaller : IWorkloadPackInstaller
    {
        private readonly IReporter _reporter;
        private readonly string _workloadMetadataDir;
        private readonly string _installedPacksDir = "InstalledPacks";
        protected readonly string _dotnetDir;
        protected readonly DirectoryPath _tempPackagesDir;
        private readonly INuGetPackageDownloader _nugetPackageInstaller;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly NetSdkManagedInstallationRecordRepository _installationRecordRepository;

        public NetSdkManagedInstaller(
            IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir =  null)
        {
            _dotnetDir = dotnetDir ?? EnvironmentProvider.GetDotnetExeDirectory();
            _tempPackagesDir = new DirectoryPath(Path.Combine(_dotnetDir, "metadata", "temp"));
            _nugetPackageInstaller = nugetPackageDownloader ?? new NuGetPackageDownloader(_tempPackagesDir);
            _workloadMetadataDir = Path.Combine(_dotnetDir, "metadata", "workloads");
            _reporter = reporter;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _installationRecordRepository = new NetSdkManagedInstallationRecordRepository(_dotnetDir);
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

        public void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false)
        {
            if (useOfflineCache)
            {
                throw new NotImplementedException();
            }

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
                            var packagePath = _nugetPackageInstaller.DownloadPackageAsync(new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)).Result;
                            tempFilesToDelete.Add(packagePath);

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

        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand) => throw new NotImplementedException();

        public void DownloadToOfflineCache(IEnumerable<string> manifests) => throw new NotImplementedException();

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

                    foreach (var unneededRecord in unneededBandRecords)
                    {
                        File.Delete(unneededRecord);
                    }

                    if (!bandRecords.Except(unneededBandRecords).Any())
                    {
                        Directory.Delete(packVersionDir);
                        var deletablePack = GetPackInfo(packVersionDir);
                        DeletePack(deletablePack);
                    }
                }

                if (!Directory.GetFileSystemEntries(packIdDir).Any())
                {
                    Directory.Delete(packIdDir);
                }
            }
        }

        private IEnumerable<string> GetExpectedPackInstallRecords(SdkFeatureBand sdkFeatureBand)
        {
            var installedWorkloads = _installationRecordRepository.GetInstalledWorkloads(sdkFeatureBand);
            return installedWorkloads
                .SelectMany(workload => _workloadResolver.GetPacksInWorkload(workload))
                .Select(pack => _workloadResolver.TryGetPackInfo(pack))
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
            File.Create(path);
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
            var packInstallRecordDir = Path.Combine(_workloadMetadataDir, _installedPacksDir, "v1", packInfo.Id, packInfo.Version);
            return Directory.Exists(packInstallRecordDir) && Directory.GetFiles(packInstallRecordDir).Any();
        }

        private bool IsSingleFilePack(PackInfo packInfo) => packInfo.Kind.Equals(WorkloadPackKind.Library) || packInfo.Kind.Equals(WorkloadPackKind.Template);
    }
}
