// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.DotNet.Cli.NuGetPackageInstaller;
using NuGet.Versioning;
using Microsoft.DotNet.ToolPackage;
using System;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NetSdkManagedInstaller : IPackWorkloadInstaller
    {
        private readonly IReporter _reporter;
        private readonly string _manifestsDir;
        private readonly string _installedWorkloadDir = ".installedworkloads";
        private readonly string _installedPacksDir = ".installedpacks";
        private INuGetPackageInstaller _nugetPackageInstaller;

        public NetSdkManagedInstaller(
            IReporter reporter,
            INuGetPackageInstaller nugetPackageInstaller = null,
            string dotnetDir =  null)
        {
            var packagesPath = Path.Combine(EnvironmentProvider.GetUserHomeDirectory(), "dotnetsdk");
            _nugetPackageInstaller = nugetPackageInstaller ?? new NuGetPackageInstaller(packagesPath, sourceUrl: "https://pkgs.dev.azure.com/azure-public/vside/_packaging/xamarin-impl/nuget/v3/index.json");
            _manifestsDir = Path.Combine(dotnetDir ?? EnvironmentProvider.GetDotnetExeDirectory(), "sdk-manifests");
            _reporter = reporter;
        }

        public override void InstallWorkloadPack(PackInfo packInfo, string featureBand, bool useOfflineCache = false)
        {
            if (useOfflineCache)
            {
                throw new NotImplementedException();
            }

            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingPackVersionMessage, packInfo.Id, packInfo.Version));
            var nupkgsToDelete = new List<string>();
            try
            {
                TransactionalAction.Run(
                    action: () =>
                    {
                        if (!PackIsInstalled(packInfo))
                        {
                            var packPath = _nugetPackageInstaller.InstallPackageAsync(new PackageId(packInfo.Id), new NuGetVersion(packInfo.Version)).Result;
                            nupkgsToDelete.Add(packPath);
                            var packFiles = _nugetPackageInstaller.ExtractPackageAsync(packPath, packInfo.Path).Result;
                        }
                        else
                        {
                            _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadPackAlreadyInstalledMessage, packInfo.Id, packInfo.Version));
                        }

                        WritePackInstallationRecord(packInfo, featureBand);
                    },
                    rollback: () => {
                        RollBackWorkloadPackInstall(packInfo, featureBand);
                    });
            }
            finally
            {
                // Delete leftover nupkgs that have been extracted
                foreach (var package in nupkgsToDelete)
                {
                    if (File.Exists(package))
                    {
                        File.Delete(package);
                    }
                }
            }
        }

        public override void RollBackWorkloadPackInstall(PackInfo packInfo, string featureBand)
        {
            DeletePack(packInfo);
            DeletePackInstallationRecord(packInfo, featureBand);
        }

        public override void InstallWorkloadManifest(string manifestId, string manifestVersion, string sdkFeatureBand) => throw new NotImplementedException();

        public override void DownloadToOfflineCache(IReadOnlyCollection<string> manifests) => throw new NotImplementedException();

        public override void GarbageCollectInstalledWorkloadPacks() => throw new NotImplementedException();

        public override IReadOnlyCollection<string> GetFeatureBandsWithInstallationRecords()
        {
            var bands = Directory.EnumerateDirectories(_manifestsDir);
            return bands
                .Where(band => Directory.Exists(Path.Combine(_manifestsDir, band, _installedWorkloadDir)))
                .Select(path => Path.GetFileName(path))
                .ToList().AsReadOnly();
        }

        public override IReadOnlyCollection<string> GetInstalledWorkloads(string featureBand)
        {
            var path = Path.Combine(_manifestsDir, featureBand, _installedWorkloadDir);
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

        public override void WriteWorkloadInstallationRecord(string workloadId, string featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingWorkloadInstallRecordMessage, workloadId));
            var path = Path.Combine(_manifestsDir, featureBand, _installedWorkloadDir, workloadId);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, string.Empty);
        }

        public override void DeleteWorkloadInstallationRecord(string workloadId, string featureBand)
        {
            var path = Path.Combine(_manifestsDir, featureBand, _installedWorkloadDir, workloadId);
            File.Delete(path);
        }

        private bool PackIsInstalled(PackInfo packInfo)
        {
            return Directory.Exists(packInfo.Path);
        }

        private void DeletePack(PackInfo packInfo)
        {
            if (PackIsInstalled(packInfo))
            {
                Directory.Delete(Path.GetDirectoryName(packInfo.Path), true);
            }
        }

        private string GetPackInstallRecordPath(PackInfo packInfo, string featureBand) =>
            Path.Combine(_manifestsDir, _installedPacksDir, "v1", packInfo.Id, packInfo.Version, featureBand);

        private void WritePackInstallationRecord(PackInfo packInfo, string featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingPackInstallRecordMessage, packInfo.Id, packInfo.Version));
            var path = GetPackInstallRecordPath(packInfo, featureBand);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, string.Empty);
        }

        private void DeletePackInstallationRecord(PackInfo packInfo, string featureBand)
        {
            var path = GetPackInstallRecordPath(packInfo, featureBand);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
