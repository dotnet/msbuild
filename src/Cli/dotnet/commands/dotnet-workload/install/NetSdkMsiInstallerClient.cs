// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32.Msi;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    [SupportedOSPlatform("windows")]
    internal class NetSdkMsiInstallerClient : MsiInstallerBase, IWorkloadPackInstaller
    {
        private INuGetPackageDownloader _nugetPackageDownloader;

        private SdkFeatureBand _sdkFeatureBand;

        private IWorkloadResolver _workloadResolver;

        private readonly PackageSourceLocation _packageSourceLocation;

        private readonly string _dependent;

        public int ExitCode => Restart ? unchecked((int)Error.SUCCESS_REBOOT_REQUIRED) : unchecked((int)Error.SUCCESS);

        public NetSdkMsiInstallerClient(InstallElevationContextBase elevationContext,
            ISetupLogger logger,
            IWorkloadResolver workloadResolver,
            SdkFeatureBand sdkFeatureBand,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null) : base(elevationContext, logger, reporter)
        {
            _packageSourceLocation = packageSourceLocation;
            _nugetPackageDownloader = nugetPackageDownloader;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            Log?.LogMessage($"Executing: {CurrentProcess.GetCommandLine()}, PID: {CurrentProcess.Id}, PPID: {ParentProcess.Id}");
            Log?.LogMessage($"{nameof(IsElevated)}: {IsElevated}");
            Log?.LogMessage($"{nameof(Is64BitProcess)}: {Is64BitProcess}");
            Log?.LogMessage($"{nameof(RebootPending)}: {RebootPending}");
            Log?.LogMessage($"{nameof(ProcessorArchitecture)}: {ProcessorArchitecture}");
            Log?.LogMessage($"{nameof(HostArchitecture)}: {HostArchitecture}");
            Log?.LogMessage($"{nameof(SdkDirectory)}: {SdkDirectory}");
            Log?.LogMessage($"SDK feature band: {_sdkFeatureBand}");

            if (IsElevated)
            {
                // Turn off automatic updates. We don't want MU to potentially patch the SDK 
                // and it also reduces the risk of hitting ERROR_INSTALL_ALREADY_RUNNING.
                UpdateAgent.Stop();
            }
        }

        public void DownloadToOfflineCache(PackInfo packInfo, DirectoryPath cachePath, bool includePreviews)
        {
            // Determine the MSI payload package ID based on the host architecture, pack ID and pack version.
            string msiPackageId = GetMsiPackageId(packInfo);

            Reporter.WriteLine(string.Format(LocalizableStrings.DownloadingPackToCacheMessage, $"{packInfo.Id} ({msiPackageId})", packInfo.Version, cachePath.Value));

            if (!Directory.Exists(cachePath.Value))
            {
                Directory.CreateDirectory(cachePath.Value);
            }

            _nugetPackageDownloader.DownloadPackageAsync(new PackageId(msiPackageId), new NuGetVersion(packInfo.Version), downloadFolder: cachePath,
                packageSourceLocation: _packageSourceLocation, includePreview: includePreviews).Wait();
        }

        /// <summary>
        /// Cleans up and removes stale workload packs.
        /// </summary>
        public void GarbageCollectInstalledWorkloadPacks()
        {
            try
            {
                Log?.LogMessage("Starting garbage collection.");
                IEnumerable<SdkFeatureBand> installedFeatureBands = GetInstalledFeatureBands();
                IEnumerable<WorkloadId> installedWorkloads = RecordRepository.GetInstalledWorkloads(_sdkFeatureBand);
                IEnumerable<PackInfo> expectedWorkloadPacks = installedWorkloads
                    .SelectMany(workload => _workloadResolver.GetPacksInWorkload(workload))
                    .Select(pack => _workloadResolver.TryGetPackInfo(pack))
                    .Where(pack => pack != null);
                IEnumerable<WorkloadPackId> expectedPackIds = expectedWorkloadPacks.Select(p => p.Id);

                foreach (PackInfo expectedPack in expectedWorkloadPacks)
                {
                    Log?.LogMessage($"Expected workload pack, ID: {expectedPack.ResolvedPackageId}, version: {expectedPack.Version}.");
                }

                foreach (SdkFeatureBand installedFeatureBand in installedFeatureBands)
                {
                    Log?.LogMessage($"Installed feature band: {installedFeatureBand}");
                }

                IEnumerable<WorkloadPackRecord> installedWorkloadPacks = WorkloadPackRecords.Values.SelectMany(r => r);

                List<WorkloadPackRecord> packsToRemove = new List<WorkloadPackRecord>();

                // We first need to clean up the dependents and then do a pass at removing them. Querying the installed packs
                // is effectively a table scan of the registry to make sure we have accurate information and there's a
                // potential perf hit for both memory and speed when enumerating large sets of registry entries.
                foreach (WorkloadPackRecord packRecord in installedWorkloadPacks)
                {
                    DependencyProvider depProvider = new DependencyProvider(packRecord.ProviderKeyName);

                    // Find all the dependents that look like they belong to SDKs. We only care
                    // about dependents that match the SDK host we're running under. For example, an x86 SDK should not be
                    // modifying the x64 MSI dependents.
                    IEnumerable<string> sdkDependents = depProvider.Dependents
                        .Where(d => d.StartsWith($"{DependentPrefix}"))
                        .Where(d => d.EndsWith($",{HostArchitecture}"));

                    foreach (string dependent in sdkDependents)
                    {
                        Log?.LogMessage($"Evaluating dependent for workload pack, dependent: {dependent}, pack ID: {packRecord.PackId}, pack version: {packRecord.PackVersion}");

                        // Dependents created by the SDK should have 3 parts, for example, "Microsoft.NET.Sdk,6.0.100,x86". 
                        string[] dependentParts = dependent.Split(',');

                        if (dependentParts.Length != 3)
                        {
                            Log?.LogMessage($"Skipping dependent: {dependent}");
                            continue;
                        }

                        try
                        {
                            SdkFeatureBand dependentFeatureBand = new SdkFeatureBand(dependentParts[1]);

                            if (!installedFeatureBands.Contains(dependentFeatureBand))
                            {
                                Log?.LogMessage($"Removing dependent '{dependent}' from provider key '{depProvider.ProviderKeyName}' because its SDK feature band does not match any installed feature bands.");
                                UpdateDependent(InstallRequestType.RemoveDependent, depProvider.ProviderKeyName, dependent);
                            }

                            if (dependentFeatureBand.Equals(_sdkFeatureBand))
                            {
                                // If the current SDK feature band is listed as a dependent, we can validate
                                // the workload pack against the expected pack IDs and versions to potentially remove it.
                                if (!expectedWorkloadPacks.Where(p => packRecord.PackId.Equals(p.ResolvedPackageId))
                                    .Where(p => p.Version.Equals(packRecord.PackVersion.ToString())).Any())
                                {
                                    Log?.LogMessage($"Removing dependent '{dependent}' because the pack record does not match any expected packs.");
                                    UpdateDependent(InstallRequestType.RemoveDependent, depProvider.ProviderKeyName, dependent);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log?.LogMessage($"{e.Message}");
                            Log?.LogMessage($"{e.StackTrace}");
                            continue;
                        }
                    }

                    // Recheck the registry to see if there are any remaining dependents. If not, we can
                    // remove the workload pack. We'll add it to the list and remove the packs at the end.
                    IEnumerable<string> remainingDependents = depProvider.Dependents;

                    if (remainingDependents.Any())
                    {
                        Log?.LogMessage($"{packRecord.PackId} ({packRecord.PackVersion}) will not be removed because other dependents remain: {string.Join(", ", remainingDependents)}.");
                    }
                    else
                    {
                        packsToRemove.Add(packRecord);
                    }
                }

                foreach (WorkloadPackRecord record in packsToRemove)
                {
                    // We need to make sure the product is actually installed and that we're not dealing with an orphaned record, e.g.
                    // if a previous removal was interrupted. We can't safely clean up orphaned records because it's too expensive
                    // to query all installed components and determine the product codes associated with the component that
                    // created the record.
                    DetectState state = Detect(record.ProductCode, out string _);

                    if (state == DetectState.Present)
                    {
                        // We don't have package information and can't construct it accurately.
                        string id = $"{record.PackId}.Msi.{HostArchitecture}";

                        MsiPayload msiPayload = GetCachedMsiPayload(id, record.PackVersion.ToString());
                        VerifyPackage(msiPayload);
                        InstallAction plannedAction = GetPlannedAction(state, InstallAction.Uninstall);

                        string logFile = GetMsiLogName(record, plannedAction);

                        uint error = ExecuteWithProgress($"Removing {id} ", () => UninstallMsi(record.ProductCode, logFile));
                        ExitOnError(error, $"Failed to uninstall {msiPayload.MsiPath}.");
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public InstallationUnit GetInstallationUnit() => InstallationUnit.Packs;

        public IEnumerable<(WorkloadPackId, string)> GetInstalledPacks(SdkFeatureBand sdkFeatureBand)
        {
            string dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            return WorkloadPackRecords.Values.SelectMany(packRecord => packRecord)
                .Where(packRecord => new DependencyProvider(packRecord.ProviderKeyName).Dependents.Contains(dependent))
                .Select(packRecord => (packRecord.PackId, packRecord.PackVersion.ToString()));
        }

        public IWorkloadPackInstaller GetPackInstaller() => this;

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository() => RecordRepository;

        public IWorkloadInstaller GetWorkloadInstaller() => throw new InvalidOperationException($"{GetType()} is not a workload installer.");

        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            try
            {
                Log?.LogMessage($"Installing manifest, {nameof(manifestId)}: {manifestId}, {nameof(manifestVersion)}: {manifestVersion}, {nameof(sdkFeatureBand)}: {sdkFeatureBand}");

                // Resolve the package ID for the manifest payload package
                string msiPackageId = WorkloadManifestUpdater.GetManifestPackageId(sdkFeatureBand, manifestId).ToString();
                string msiPackageVersion = $"{manifestVersion}";

                Log?.LogMessage($"Resolving {manifestId} ({manifestVersion}) to {msiPackageId}.{msiPackageVersion}.");

                // Retrieve the payload from the MSI package cache. 
                MsiPayload msiPayload = GetCachedMsiPayload(msiPackageId, msiPackageVersion);
                VerifyPackage(msiPayload);
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void RepairWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            try
            {
                // Determine the MSI payload package ID based on the host architecture, pack ID and pack version.
                string msiPackageId = GetMsiPackageId(packInfo);

                // Retrieve the payload from the MSI package cache. 
                MsiPayload msiPayload = GetCachedMsiPayload(msiPackageId, packInfo.Version);
                VerifyPackage(msiPayload);
                DetectState state = Detect(msiPayload.Manifest.ProductCode, out string _);
                PlanAndExecute(msiPayload, packInfo, state, InstallAction.Repair);

                // Update the reference count against the MSI.
                UpdateDependent(InstallRequestType.AddDependent, msiPayload.Manifest.ProviderKeyName, _dependent);
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            try
            {
                // Determine the MSI payload package ID based on the host architecture, pack ID and pack version.
                string msiPackageId = GetMsiPackageId(packInfo);

                // Retrieve the payload from the MSI package cache. 
                MsiPayload msiPayload = GetCachedMsiPayload(msiPackageId, packInfo.Version);
                VerifyPackage(msiPayload);
                DetectState state = Detect(msiPayload.Manifest.ProductCode, out string _);
                PlanAndExecute(msiPayload, packInfo, state, InstallAction.Install);

                // Update the reference count against the MSI.
                UpdateDependent(InstallRequestType.AddDependent, msiPayload.Manifest.ProviderKeyName, _dependent);
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            Log?.LogMessage($"Rolling back workload pack.");
            LogPackInfo(packInfo);
        }

        public void Shutdown()
        {
            Log?.LogMessage("Shutting down");

            if (IsElevated)
            {
                UpdateAgent.Start();
            }
            else if (IsClient && Dispatcher != null && Dispatcher.IsConnected)
            {
                InstallResponseMessage response = Dispatcher.SendShutdownRequest();
            }

            Log?.LogMessage("Shutdown completed.");
            Log?.LogMessage($"Restart required: {Restart}");
        }

        private void LogPackInfo(PackInfo packInfo)
        {
            Log?.LogMessage($"{nameof(PackInfo)}: {nameof(packInfo.Id)}: {packInfo.Id}, {nameof(packInfo.Kind)}: {packInfo.Kind}, {nameof(packInfo.Version)}: {packInfo.Version}, {nameof(packInfo.ResolvedPackageId)}: {packInfo.ResolvedPackageId}");
        }

        /// <summary>
        /// Determines the state of the specified product.
        /// </summary>
        /// <param name="productCode">The product code of the MSI to detect.</param>
        /// <param name="installedVersion">If detected, contains the version of the installed MSI.</param>
        /// <returns>The detect state of the specified MSI.</returns>
        private DetectState Detect(string productCode, out string installedVersion)
        {
            uint error = WindowsInstaller.GetProductInfo(productCode, InstallProperty.VERSIONSTRING, out installedVersion);

            DetectState state = error == Error.SUCCESS ? DetectState.Present
                : (error == Error.UNKNOWN_PRODUCT) || (error == Error.UNKNOWN_PROPERTY) ? DetectState.Absent
                : DetectState.Unknown;

            ExitOnError(state == DetectState.Unknown, error, $"Failed to detect MSI package with ProductCode={productCode}.");
            Log?.LogMessage($"Detected package, ProductCode: {productCode}, version: {(string.IsNullOrWhiteSpace(installedVersion) ? "n/a" : $"{installedVersion}")}, state: {state}.");

            return state;
        }

        /// <summary>
        /// Derives the MSI package ID from the specified pack information based on the bitness of
        /// the SDK host.
        /// </summary>
        /// <param name="packInfo">The pack information used to generate the package ID.</param>
        /// <returns>The ID of the NuGet package containing the MSI corresponding to the pack.</returns>
        private string GetMsiPackageId(PackInfo packInfo)
        {
            return $"{packInfo.ResolvedPackageId}.Msi.{HostArchitecture}";
        }

        /// <summary>
        /// Downloads the NuGet package carrying the MSI and JSON manifest and extracts it locally to
        /// a temporary folder.
        /// </summary>
        /// <param name="packageId">The ID of the package to download.</param>
        /// <param name="packageVersion">The version of the package to download.</param>
        /// <returns>The directory where the package was extracted.</returns>
        private string DownloadAndExtractPackage(string packageId, string packageVersion)
        {
            string packagePath = _nugetPackageDownloader.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion),
                _packageSourceLocation).Result;

            Log?.LogMessage($"Downloaded {packageId} ({packageVersion}) to '{packagePath}");

            // Extract the contents to a random folder to avoid potential file injection/hijacking 
            // shenanigans before moving it to the final cache directory.
            string extractionDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractionDirectory);
            Log?.LogMessage($"Extracting '{packageId}' to '{extractionDirectory}'");
            _ = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(extractionDirectory)).Result;

            return extractionDirectory;
        }

        /// <summary>
        /// Gets a set of all the installed SDK feature bands.
        /// </summary>
        /// <returns>A List of all the installed SDK feature bands.</returns>
        private IEnumerable<SdkFeatureBand> GetInstalledFeatureBands()
        {
            HashSet<SdkFeatureBand> installedFeatureBands = new();
            foreach (string sdkVersion in GetInstalledSdkVersions())
            {
                try
                {
                    installedFeatureBands.Add(new SdkFeatureBand(sdkVersion));
                }
                catch (Exception e)
                {
                    Log?.LogMessage($"Failed to map SDK version {sdkVersion} to a feature band. ({e.Message})");
                }
            }

            return installedFeatureBands;
        }

        /// <summary>
        /// Tries to retrieve the MSI payload for the specified package ID and version from
        /// the MSI package cache. 
        /// </summary>
        /// <param name="packageId">The ID of the payload package.</param>
        /// <param name="packageVersion">The version of the payload package.</param>
        /// <returns>The MSI payload or <see langword="null"/> if unsuccessful.</returns>
        private MsiPayload GetCachedMsiPayload(string packageId, string packageVersion)
        {
            if (!Cache.TryGetPayloadFromCache(packageId, packageVersion, out MsiPayload msiPayload))
            {
                // If it's not fully cached, download and extract the payload package into a temporary location
                // and try to cache it again. We DO NOT trust partially cached packages.
                string extractedPackageRootPath = DownloadAndExtractPackage(packageId, packageVersion);
                string manifestPath = Path.Combine(extractedPackageRootPath, "data", "msi.json");

                Cache.CachePayload(packageId, packageVersion, manifestPath);
                Directory.Delete(extractedPackageRootPath, recursive: true);

                if (!Cache.TryGetPayloadFromCache(packageId, packageVersion, out msiPayload))
                {
                    ExitOnError(Error.FILE_NOT_FOUND, $"Failed to retrieve MSI payload from cache, Id: {packageId}, version: {packageVersion}.");
                }
            }

            return msiPayload;
        }

        /// <summary>
        /// Determines the final <see cref="InstallAction"/> based on the requested action and detection
        /// state of the MSI.
        /// </summary>
        /// <param name="state">The detected state of the MSI.</param>
        /// <param name="requestAction">The requested action to perform.</param>
        /// <returns>The final action that should be performed.</returns>
        private InstallAction GetPlannedAction(DetectState state, InstallAction requestAction)
        {
            InstallAction plannedAction = InstallAction.None;

            if (state == DetectState.Absent)
            {
                if (requestAction == InstallAction.Install || requestAction == InstallAction.Repair)
                {
                    plannedAction = InstallAction.Install;
                }
            }
            else if (state == DetectState.Present)
            {
                if (requestAction == InstallAction.Repair || requestAction == InstallAction.Uninstall)
                {
                    plannedAction = requestAction;
                }
            }

            return plannedAction;
        }

        /// <summary>
        /// Executes a windows installer package using the provided MSI payload, the state of the package and the desired install action.
        /// </summary>
        /// <param name="msiPayload"></param>
        /// <param name="packInfo"></param>
        /// <param name="state"></param>
        /// <param name="requestAction"></param>
        private void PlanAndExecute(MsiPayload msiPayload, PackInfo packInfo, DetectState state, InstallAction requestAction)
        {
            uint error = Error.SUCCESS;
            InstallAction plannedAction = GetPlannedAction(state, requestAction);

            Log?.LogMessage($"Execute package, id: {packInfo.ResolvedPackageId}, state: {state}, requested: {requestAction}, execute: {plannedAction}");
            string logFile = GetMsiLogName(packInfo, plannedAction);

            if (plannedAction == InstallAction.Install)
            {
                error = ExecuteWithProgress($"Installing {packInfo.ResolvedPackageId}", () => InstallMsi(msiPayload.MsiPath, logFile));
                ExitOnError(error, $"Failed to install {msiPayload.MsiPath}.");
            }
            else if (plannedAction == InstallAction.Uninstall)
            {
                error = ExecuteWithProgress($"Removing {packInfo.ResolvedPackageId} ", () => UninstallMsi(msiPayload.Manifest.ProductCode, logFile));
                ExitOnError(error, $"Failed to uninstall {msiPayload.MsiPath}.");
            }
            else if (plannedAction == InstallAction.Repair)
            {
                error = ExecuteWithProgress($"Repairing {packInfo.ResolvedPackageId} ", () => RepairMsi(msiPayload.Manifest.ProductCode, logFile));
                ExitOnError(error, $"Failed to repair {msiPayload.MsiPath}.");
            }
        }

        /// <summary>
        /// Executes the install delegate using a separate task while reporting progress on the current thread.
        /// </summary>
        /// <param name="progressLabel">A label to be written before writing progress information.</param>
        /// <param name="installDelegate">The function to execute.</param>
        private uint ExecuteWithProgress(string progressLabel, Func<uint> installDelegate)
        {
            uint error = Error.SUCCESS;

            Task<uint> installTask = Task.Run<uint>(installDelegate);
            Reporter.Write($"{progressLabel}...");

            // This is just simple progress, a.k.a., a series of dots. Ideally we need to wire up the external
            // UI handler. Since that potentially runs on the elevated server instance, we'd need to create
            // an additional thread for handling progress reports from the server.
            while (!installTask.IsCompleted)
            {
                Reporter.Write(".");
                Thread.Sleep(500);
            }

            if (installTask.IsFaulted)
            {
                Reporter.WriteLine(" Failed");
                throw installTask.Exception.InnerException;
            }

            error = installTask.Result;

            if (!Error.Success(error))
            {
                Reporter.WriteLine(" Failed");
            }
            else
            {
                Reporter.WriteLine($" Done");
            }

            return error;
        }

        /// <summary>
        /// Verifies that the <see cref="MsiPayload"/> refers to a valid Windows Installer package (MSI).
        /// </summary>
        /// <param name="msiPayload">The payload to verify.</param>
        private void VerifyPackage(MsiPayload msiPayload)
        {
            uint error = WindowsInstaller.VerifyPackage(msiPayload.MsiPath);
            ExitOnError(error, $"Failed to verify package: {msiPayload.MsiPath}.");
        }

        /// <summary>
        /// Creates a new <see cref="NetSdkMsiInstallerClient"/> instance. If the current host process is not elevated, 
        /// the elevated server process will also be started by running an additional command.
        /// </summary>
        /// <param name="nugetPackageDownloader"></param>
        /// <param name="verbosity"></param>
        /// <param name="packageSourceLocation"></param>
        /// <returns></returns>
        public static NetSdkMsiInstallerClient Create(
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null)
        {
            TimestampedFileLogger logger = new(Path.Combine(Path.GetTempPath(), $"Microsoft.NET_Workload_Install_{DateTime.Now:yyyyMMdd_HHmmss}.log"));
            InstallClientElevationContext elevationContext = new(logger);

            return new NetSdkMsiInstallerClient(elevationContext, logger, workloadResolver, sdkFeatureBand, nugetPackageDownloader,
                verbosity, packageSourceLocation, reporter);
        }
    }
}
