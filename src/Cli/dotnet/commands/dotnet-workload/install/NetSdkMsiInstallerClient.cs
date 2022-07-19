// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    [SupportedOSPlatform("windows")]
    internal partial class NetSdkMsiInstallerClient : MsiInstallerBase, IInstaller
    {
        private INuGetPackageDownloader _nugetPackageDownloader;

        private SdkFeatureBand _sdkFeatureBand;

        private IWorkloadResolver _workloadResolver;

        private bool _shutdown;

        private readonly PackageSourceLocation _packageSourceLocation;

        private readonly string _dependent;

        public int ExitCode => Restart ? unchecked((int)Error.SUCCESS_REBOOT_REQUIRED) : unchecked((int)Error.SUCCESS);

        public NetSdkMsiInstallerClient(InstallElevationContextBase elevationContext,
            ISetupLogger logger,
            bool verifySignatures,
            IWorkloadResolver workloadResolver,
            SdkFeatureBand sdkFeatureBand,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null) : base(elevationContext, logger, verifySignatures, reporter)
        {
            _packageSourceLocation = packageSourceLocation;
            _nugetPackageDownloader = nugetPackageDownloader;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Log?.LogMessage($"Executing: {Windows.GetProcessCommandLine()}, PID: {CurrentProcess.Id}, PPID: {ParentProcess.Id}");
            Log?.LogMessage($"{nameof(IsElevated)}: {IsElevated}");
            Log?.LogMessage($"{nameof(Is64BitProcess)}: {Is64BitProcess}");
            Log?.LogMessage($"{nameof(RebootPending)}: {RebootPending}");
            Log?.LogMessage($"{nameof(ProcessorArchitecture)}: {ProcessorArchitecture}");
            Log?.LogMessage($"{nameof(HostArchitecture)}: {HostArchitecture}");
            Log?.LogMessage($"{nameof(SdkDirectory)}: {SdkDirectory}");
            Log?.LogMessage($"{nameof(VerifySignatures)}: {VerifySignatures}");
            Log?.LogMessage($"SDK feature band: {_sdkFeatureBand}");

            if (IsElevated)
            {
                // Turn off automatic updates. We don't want MU to potentially patch the SDK
                // and it also reduces the risk of hitting ERROR_INSTALL_ALREADY_RUNNING.
                UpdateAgent.Stop();
            }
        }

        public void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver)
        {
            _workloadResolver = workloadResolver;
        }

        private IEnumerable<(WorkloadPackId Id, string Version)> GetInstalledPacks(SdkFeatureBand sdkFeatureBand)
        {
            string dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            return GetWorkloadPackRecords()
                .Where(packRecord => new DependencyProvider(packRecord.ProviderKeyName).Dependents.Contains(dependent))
                .SelectMany(packRecord => packRecord.InstalledPacks)
                .Select(p => (p.id, p.version.ToString()));
        }

        public IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems)
        {
            IEnumerable<WorkloadDownload> msis = GetMsisForWorkloads(workloadIds);
            if (!includeInstalledItems)
            {
                HashSet<(string id, string version)> installedItems = new(GetInstalledPacks(sdkFeatureBand).Select(t => (t.Id.ToString(), t.Version)));
                msis = msis.Where(m => !installedItems.Contains((m.Id, m.NuGetPackageVersion)));
            }

            return msis.ToList(); ;
        }

        /// <summary>
        /// Cleans up and removes stale workload packs.
        /// </summary>
        public void GarbageCollectInstalledWorkloadPacks(DirectoryPath? offlineCache = null)
        {
            try
            {
                ReportPendingReboot();
                Log?.LogMessage("Starting garbage collection.");
                IEnumerable<SdkFeatureBand> installedFeatureBands = GetInstalledFeatureBands();
                IEnumerable<WorkloadId> installedWorkloads = RecordRepository.GetInstalledWorkloads(_sdkFeatureBand);
                Dictionary<(WorkloadPackId id, string version),PackInfo> expectedWorkloadPacks = installedWorkloads
                    .SelectMany(workload => _workloadResolver.GetPacksInWorkload(workload))
                    .Distinct()
                    .Select(pack => _workloadResolver.TryGetPackInfo(pack))
                    .Where(pack => pack != null)
                    .ToDictionary(p => (new WorkloadPackId(p.ResolvedPackageId), p.Version));

                foreach (PackInfo expectedPack in expectedWorkloadPacks.Values)
                {
                    Log?.LogMessage($"Expected workload pack, ID: {expectedPack.ResolvedPackageId}, version: {expectedPack.Version}.");
                }

                foreach (SdkFeatureBand installedFeatureBand in installedFeatureBands)
                {
                    Log?.LogMessage($"Installed feature band: {installedFeatureBand}");
                }

                IEnumerable<WorkloadPackRecord> installedWorkloadPacks = GetWorkloadPackRecords();

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
                        Log?.LogMessage($"Evaluating dependent for workload pack, dependent: {dependent}, MSI ID: {packRecord.MsiId}, MSI version: {packRecord.MsiNuGetVersion}");

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
                                // the workload packs against the expected pack IDs and versions to potentially remove it.
                                if (packRecord.InstalledPacks.All(p => !expectedWorkloadPacks.ContainsKey((p.id, p.version.ToString()))))
                                {
                                    //  None of the packs installed by this MSI are necessary any longer for this feature band, so we can remove the reference count
                                    Log?.LogMessage($"Removing dependent '{dependent}' because the pack record(s) do not match any expected packs.");
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
                        Log?.LogMessage($"{packRecord.MsiId} ({packRecord.MsiNuGetVersion}) will not be removed because other dependents remain: {string.Join(", ", remainingDependents)}.");
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
                    DetectState state = DetectPackage(record.ProductCode, out Version _);

                    if (state == DetectState.Present)
                    {
                        // Manually construct the MSI payload package details
                        string id = $"{record.MsiId}.Msi.{HostArchitecture}";
                        MsiPayload msi = GetCachedMsiPayload(id, record.MsiNuGetVersion.ToString(), offlineCache);

                        // Make sure the package we have in the cache matches with the record. If it doesn't, we'll do the uninstall
                        // the hard way
                        if (!string.Equals(record.ProductCode, msi.ProductCode, StringComparison.OrdinalIgnoreCase))
                        {
                            Log?.LogMessage($"ProductCode mismatch! Cached package: {msi.ProductCode}, pack record: {record.ProductCode}.");
                            string logFile = GetMsiLogName(record, InstallAction.Uninstall);
                            uint error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressUninstall, id), () => UninstallMsi(record.ProductCode, logFile));
                            ExitOnError(error, $"Failed to uninstall {msi.MsiPath}.");
                        }
                        else
                        {
                            // No need to plan. We know that there are no other dependents, the MSI is installed and we
                            // want to remove it.
                            VerifyPackage(msi);
                            ExecutePackage(msi, InstallAction.Uninstall);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository() => RecordRepository;

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null, bool isRollback = false)
        {
            try
            {
                transactionContext.Run(
                    action: () =>
                    {
                        InstallWorkloadManifestImplementation(manifestUpdate, offlineCache, isRollback);
                    },
                    rollback: () =>
                    {
                        InstallWorkloadManifestImplementation(manifestUpdate.Reverse(), offlineCache: null, isRollback: true);
                    });
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        void InstallWorkloadManifestImplementation(ManifestVersionUpdate manifestUpdate, DirectoryPath? offlineCache = null, bool isRollback = false)
        {
            ReportPendingReboot();

            // Rolling back a manifest update after a successful install is essentially a downgrade, which is blocked so we have to
            // treat it as a special case and is different from the install failing and rolling that back, though depending where the install
            // failed, it may have removed the old product already.
            Log?.LogMessage($"Installing manifest: Id: {manifestUpdate.ManifestId}, version: {manifestUpdate.NewVersion}, feature band: {manifestUpdate.NewFeatureBand}, rollback: {isRollback}.");

            // Resolve the package ID for the manifest payload package
            string msiPackageId = GetManifestPackageId(manifestUpdate.ManifestId, new SdkFeatureBand(manifestUpdate.NewFeatureBand)).ToString();
            string msiPackageVersion = $"{manifestUpdate.NewVersion}";

            Log?.LogMessage($"Resolving {manifestUpdate.ManifestId} ({manifestUpdate.NewVersion}) to {msiPackageId} ({msiPackageVersion}).");

            // Retrieve the payload from the MSI package cache.
            MsiPayload msi = GetCachedMsiPayload(msiPackageId, msiPackageVersion, offlineCache);
            VerifyPackage(msi);
            DetectState state = DetectPackage(msi.ProductCode, out Version installedVersion);
            InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Install, installedVersion, out IEnumerable<string> relatedProducts);

            // If we've detected a downgrade, it's possible we might be doing a rollback after the manifests were updated,
            // but another error occurred. In this case we need to try and uninstall the upgrade and then install the lower
            // version of the MSI. The downgrade can also be a deliberate rollback.
            if (plannedAction == InstallAction.Downgrade && isRollback && state == DetectState.Absent)
            {
                Log?.LogMessage($"Rolling back manifest update.");

                // The provider keys for manifest packages are stable across feature bands so we retain dependents during upgrades.
                DependencyProvider depProvider = new DependencyProvider(msi.Manifest.ProviderKeyName);

                // Try and remove the SDK dependency, but ignore any remaining dependencies since
                // we want to force the removal of the old version. The remaining dependencies and the provider
                // key won't be removed.
                UpdateDependent(InstallRequestType.RemoveDependent, msi.Manifest.ProviderKeyName, _dependent);

                // Since we don't have records for manifests, we need to try and retrieve the ProductCode of
                // the newer MSI that's installed that we want to remove using its dependency provider.
                string productCode = depProvider.ProductCode;

                if (string.IsNullOrWhiteSpace(productCode))
                {
                    // We don't know the MSI package that wrote this provider key, so if the ProductCode is missing
                    // we can't do anything else.
                    Log?.LogMessage($"Failed to retrieve the ProductCode for provider: {depProvider.ProviderKeyName}.");
                    return;
                }

                Log?.LogMessage($"Found ProductCode {productCode} registered against provider, {depProvider.ProviderKeyName}.");

                // This is a best effort. If for some reason the manifest installers were fixed, for example, manually
                // adding additional upgrade paths to work around previous faulty authoring, we may have multiple related
                // products. The best we can do is to check for at least one match and remove it and then try the rollback.
                if (!relatedProducts.Contains(productCode, StringComparer.OrdinalIgnoreCase))
                {
                    Log?.LogMessage($"Cannot rollback manifest. ProductCode does not match any detected related products.");
                    return;
                }

                string logFile = GetMsiLogName(productCode, InstallAction.Uninstall);
                uint error = UninstallMsi(productCode, logFile, ignoreDependencies: true);

                ExitOnError(error, "Failed to uninstall manifest package.");

                // Detect the package again and fall through to the original execution. If that fails, then there's nothing
                // we could have done.
                Log?.LogMessage("Replanning manifest package.");
                state = DetectPackage(msi, out Version _);
                plannedAction = PlanPackage(msi, state, InstallAction.Install, installedVersion, out IEnumerable<string> _);
            }

            ExecutePackage(msi, plannedAction);

            // Update the reference count against the MSI.
            UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
        }

        public void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            try
            {
                ReportPendingReboot();

                foreach (var aquirableMsi in GetMsisForWorkloads(workloadIds))
                {
                    // Retrieve the payload from the MSI package cache.
                    MsiPayload msi = GetCachedMsiPayload(aquirableMsi.NuGetPackageId, aquirableMsi.NuGetPackageVersion, offlineCache);
                    VerifyPackage(msi);
                    DetectState state = DetectPackage(msi, out Version installedVersion);
                    InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Repair, installedVersion, out _);
                    ExecutePackage(msi, plannedAction);

                    // Update the reference count against the MSI.
                    UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            ReportPendingReboot();

            var msisToInstall = GetMsisForWorkloads(workloadIds);

            foreach (var msiToInstall in msisToInstall)
            {
                bool shouldRollBackPack = false;

                transactionContext.Run(action: () =>
                {
                    try
                    {
                        // Retrieve the payload from the MSI package cache.
                        MsiPayload msi = GetCachedMsiPayload(msiToInstall.NuGetPackageId, msiToInstall.NuGetPackageVersion, offlineCache);
                        VerifyPackage(msi);
                        DetectState state = DetectPackage(msi, out Version installedVersion);
                        InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Install, installedVersion, out _);
                        if (plannedAction == InstallAction.Install)
                        {
                            shouldRollBackPack = true;
                        }
                        ExecutePackage(msi, plannedAction);

                        // Update the reference count against the MSI.
                        UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
                    }
                    catch (Exception e)
                    {
                        LogException(e);
                        throw;
                    }
                },
                rollback: () =>
                {
                    if (shouldRollBackPack)
                    {
                        RollBackMsiInstall(msiToInstall);
                    }
                });
                
            }
    
        }

        void RollBackMsiInstall(WorkloadDownload msiToRollback, DirectoryPath? offlineCache = null)
        {
            try
            {
                ReportPendingReboot();
                Log?.LogMessage($"Rolling back workload pack installation for {msiToRollback.NuGetPackageId}.");

                // Retrieve the payload from the MSI package cache.
                MsiPayload msi = GetCachedMsiPayload(msiToRollback.NuGetPackageId, msiToRollback.NuGetPackageVersion, offlineCache);
                VerifyPackage(msi);

                // Check the provider key first in case we were installed and we only need to remove
                // a dependent.
                DependencyProvider depProvider = new DependencyProvider(msi.Manifest.ProviderKeyName);

                // Try and remove the dependent against this SDK. If any remain we'll simply exit.
                UpdateDependent(InstallRequestType.RemoveDependent, msi.Manifest.ProviderKeyName, _dependent);

                if (depProvider.Dependents.Any())
                {
                    Log?.LogMessage($"Cannot remove pack, other dependents remain: {string.Join(", ", depProvider.Dependents)}.");
                    return;
                }

                // Make sure the MSI is actually installed.
                DetectState state = DetectPackage(msi, out Version installedVersion);
                InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Uninstall, installedVersion, out _);

                // The previous steps would have logged the final action. If the verdict is not to uninstall we can exit.
                if (plannedAction == InstallAction.Uninstall)
                {
                    ExecutePackage(msi, plannedAction);
                }

                Log?.LogMessage("Rollback completed.");
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
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
            ((TimestampedFileLogger)Log).Dispose();
            _shutdown = true;
        }

        public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            return new PackageId($"{manifestId}.Manifest-{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}");
        }

        public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
        {
            string extractionPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, true);
            }

            try
            {
                Directory.CreateDirectory(extractionPath);

                Log?.LogMessage($"Extracting '{nupkgPath}' to '{extractionPath}'");
                await _nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }

                string extractedManifestPath = Path.Combine(extractionPath, "data", "extractedManifest");
                if (Directory.Exists(extractedManifestPath))
                {
                    Log?.LogMessage($"Copying manifest from '{extractionPath}' to '{targetPath}'");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(extractedManifestPath, targetPath));
                }
                else
                {
                    string packageDataPath = Path.Combine(extractionPath, "data");
                    if (!Cache.TryGetMsiPathFromPackageData(packageDataPath, out string msiPath, out _))
                    {
                        throw new FileNotFoundException(String.Format(LocalizableStrings.ManifestMsiNotFoundInNuGetPackage, extractionPath));
                    }
                    string msiExtractionPath = Path.Combine(extractionPath, "msi");


                    _ = WindowsInstaller.SetInternalUI(InstallUILevel.None);
                    var result = WindowsInstaller.InstallProduct(msiPath, $"TARGETDIR={msiExtractionPath} ACTION=ADMIN");

                    if (result != Error.SUCCESS)
                    {
                        throw new GracefulException(String.Format(LocalizableStrings.FailedToExtractMsi, msiPath));
                    }

                    var manifestsFolder = Path.Combine(msiExtractionPath, "dotnet", "sdk-manifests");

                    string manifestFolder = null;
                    string manifestsFeatureBandFolder = Directory.GetDirectories(manifestsFolder).SingleOrDefault();
                    if (manifestsFeatureBandFolder != null)
                    {
                        manifestFolder = Directory.GetDirectories(manifestsFeatureBandFolder).SingleOrDefault();
                    }

                    if (manifestFolder == null)
                    {
                        throw new GracefulException(String.Format(LocalizableStrings.ExpectedSingleManifest, nupkgPath));
                    }

                    FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(manifestFolder, targetPath));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, true);
                }
            }
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
        private DetectState DetectPackage(string productCode, out Version installedVersion)
        {
            installedVersion = default;
            uint error = WindowsInstaller.GetProductInfo(productCode, InstallProperty.VERSIONSTRING, out string versionValue);

            DetectState state = error == Error.SUCCESS ? DetectState.Present
                : (error == Error.UNKNOWN_PRODUCT) || (error == Error.UNKNOWN_PROPERTY) ? DetectState.Absent
                : DetectState.Unknown;

            ExitOnError(state == DetectState.Unknown, error, $"DetectPackage: Failed to detect MSI package, ProductCode: {productCode}.");

            if (state == DetectState.Present)
            {
                if (!Version.TryParse(versionValue, out installedVersion))
                {
                    Log?.LogMessage($"DetectPackage: Failed to parse version: {versionValue}.");
                }
            }

            Log?.LogMessage($"DetectPackage: ProductCode: {productCode}, version: {installedVersion?.ToString() ?? "n/a"}, state: {state}.");

            return state;
        }

        /// <summary>
        /// Determines the state of the specified product.
        /// </summary>
        /// <param name="msi">The MSI package to detect.</param>
        /// <param name="installedVersion">If detected, contains the version of the installed MSI.</param>
        /// <returns>The detect state of the specified MSI.</returns>
        private DetectState DetectPackage(MsiPayload msi, out Version installedVersion)
        {
            return DetectPackage(msi.ProductCode, out installedVersion);
        }

        /// <summary>
        /// Plans the specified MSI payload based on its state and the requested install action.
        /// </summary>
        /// <param name="msi">The MSI package to plan.</param>
        /// <param name="state">The detected state of the package.</param>
        /// <param name="requestedAction">The requested action to perform.</param>
        /// <returns>The action that will be performed.</returns>
        private InstallAction PlanPackage(MsiPayload msi, DetectState state, InstallAction requestedAction, Version installedVersion, out IEnumerable<string> relatedProductCodes)
        {
            InstallAction plannedAction = InstallAction.None;
            HashSet<string> relatedProducts = new();

            Log?.LogMessage($"PlanPackage: Begin, name: {msi.Name}, version: {msi.ProductVersion}, state: {state}, installed version: {installedVersion?.ToString() ?? "n/a"}, requested: {requestedAction}.");

            // Manifest packages should support major upgrades (both the ProductCode and ProductVersion should be different) while
            // workload packs should always be SxS (ProductCode and Upgrade should be different for each version).
            //
            // We cannot discount someone generating a minor update (ProductCode remains unchanged, but ProductVersion changes),
            // so we'll detect downgrades and minor updates. For more details, see https://docs.microsoft.com/en-us/windows/win32/msi/minor-upgrades.
            if (state == DetectState.Present)
            {
                if (msi.ProductVersion < installedVersion)
                {
                    Log?.LogMessage($"PlanPackage: Downgrade detected, installed version: {installedVersion}, requested version: {msi.ProductVersion}.");
                    plannedAction = InstallAction.Downgrade;
                    state = DetectState.Superseded;
                }
                else if (msi.ProductVersion > installedVersion)
                {
                    Log?.LogMessage($"PlanPackage: Minor update detected, installed version: {installedVersion}, requested version: {msi.ProductVersion}.");
                    plannedAction = InstallAction.MinorUpdate;
                    state = DetectState.Obsolete;
                }
                else
                {
                    // If the package is installed, then we can uninstall and repair it.
                    plannedAction = (requestedAction != InstallAction.Repair) && (requestedAction != InstallAction.Uninstall) ? InstallAction.None : requestedAction;
                }
            }
            else if (state == DetectState.Absent)
            {
                // If we're absent, convert repair to install or keep install.
                plannedAction = (requestedAction == InstallAction.Repair) ? InstallAction.Install
                    : (requestedAction == InstallAction.Install) ? InstallAction.Install
                    : InstallAction.None;
            }

            // If we know the MSI is absent, there are only three outcomes when executing the package:
            //   1. We'll just do a clean install if we don't find related products so we're either brand new or SxS.
            //   2. We'll perform a major upgrade.
            //   3. We'll trigger a downgrade and likely an error since most MSIs detect and block downgrades.
            //
            // We'll process the related product information to make a determination. This is similar to what the FindRelatedProducts
            // action does when an MSI is executed.
            foreach (RelatedProduct relatedProduct in msi.RelatedProducts)
            {
                foreach (string relatedProductCode in WindowsInstaller.FindRelatedProducts(relatedProduct.UpgradeCode))
                {
                    // Ignore potentially detecting ourselves.
                    if (string.Equals(relatedProductCode, msi.ProductCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Check whether the related product is installed and retrieve its version to determine
                    // how we're related.
                    uint error = WindowsInstaller.GetProductInfo(relatedProductCode, InstallProperty.VERSIONSTRING, out string relatedVersionValue);

                    // Continue searching if the related product is not installed.
                    if (error == Error.UNKNOWN_PRODUCT || error == Error.UNKNOWN_PROPERTY)
                    {
                        continue;
                    }

                    ExitOnError(error, $"PlanPackage: Failed to retrieve version for related product: ProductCode: {relatedProductCode}.");

                    // Parse the version, but don't try to catch any errors. If the version is invalid we want to fail
                    // because we can't compare invalid versions to see whether it's excluded by the VersionMin and VersionMax
                    // columns from the Upgrade table.
                    Version relatedVersion = Version.Parse(relatedVersionValue);

                    if (relatedProduct.ExcludesMinVersion(relatedVersion) || relatedProduct.ExcludesMaxVersion(relatedVersion))
                    {
                        continue;
                    }

                    // Check if the related product contains a matching language code (LCID). If we don't have any languages,
                    // all languages are detectable as related and we can ignore the msidbUpgradeAttributesLanguagesExclusive flag.
                    if (relatedProduct.Languages.Any())
                    {
                        string relatedLanguage = "0";
                        error = WindowsInstaller.GetProductInfo(relatedProductCode, InstallProperty.LANGUAGE, out relatedLanguage);

                        if (int.TryParse(relatedLanguage, out int lcid))
                        {
                            if (relatedProduct.ExcludesLanguage(lcid))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            Log?.LogMessage($"PlanPackage: Failed to read Language property for related product, ProductCode: {relatedProductCode}. The related product will be skipped.");
                            continue;
                        }
                    }

                    relatedProducts.Add(relatedProductCode);
                    plannedAction = InstallAction.MajorUpgrade;

                    if (relatedProduct.Attributes.HasFlag(UpgradeAttributes.OnlyDetect) && (state == DetectState.Absent))
                    {
                        // If we're not installed, but detect-only related, it's very likely that
                        // that we'd trigger a downgrade launch condition. We can't know for sure, but
                        // generally that's the most common use for detect-only entries.
                        plannedAction = InstallAction.Downgrade;
                    }

                    Log?.LogMessage($"PlanPackage: Detected related product, ProductCode: {relatedProductCode}, version: {relatedVersion}, attributes: {relatedProduct.Attributes}, planned action: {plannedAction}.");
                }
            }

            Log?.LogMessage($"PlanPackage: Completed, name: {msi.Name}, version: {msi.ProductVersion}, state: {state}, installed version: {installedVersion?.ToString() ?? "n/a"}, requested: {requestedAction}, planned: {plannedAction}.");

            relatedProductCodes = relatedProducts.Select(p => p);
            return plannedAction;
        }

        /// <summary>
        /// Derives the MSI package ID from the specified pack information based on the bitness of
        /// the SDK host.
        /// </summary>
        /// <param name="packInfo">The pack information used to generate the package ID.</param>
        /// <returns>The ID of the NuGet package containing the MSI corresponding to the pack.</returns>
        private static string GetMsiPackageId(PackInfo packInfo)
        {
            return $"{packInfo.ResolvedPackageId}.Msi.{HostArchitecture}";
        }

        /// <summary>
        /// Extracts the MSI and JSON manifest using the NuGet package in the offline cache to a temporary
        /// folder or downloads a copy before extracting it.
        /// </summary>
        /// <param name="packageId">The ID of the package to download.</param>
        /// <param name="packageVersion">The version of the package to download.</param>
        /// <param name="offlineCache">The path of the offline package cache. If <see langword="null"/>, the package
        /// is downloaded.</param>
        /// <returns>The directory where the package was extracted.</returns>
        /// <exception cref="FileNotFoundException" />
        private string ExtractPackage(string packageId, string packageVersion, DirectoryPath? offlineCache)
        {
            string packagePath;

            if (offlineCache == null || !offlineCache.HasValue)
            {
                Reporter.WriteLine($"Downloading {packageId} ({packageVersion})");
                packagePath = _nugetPackageDownloader.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion),
                    _packageSourceLocation).Result;
                Log?.LogMessage($"Downloaded {packageId} ({packageVersion}) to '{packagePath}");
            }
            else
            {
                packagePath = Path.Combine(offlineCache.Value.Value, $"{packageId}.{packageVersion}.nupkg");
                Log?.LogMessage($"Using offline cache, package: {packagePath}");
            }

            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException(string.Format(LocalizableStrings.CacheMissingPackage, packageId, packageVersion, offlineCache));
            }

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
        /// <param name="offlineCache">The path to the offline cache. When <see langword="null"/>, packages are downloaded using the
        /// existing package feeds.</param>
        /// <returns>The MSI payload or <see langword="null"/> if unsuccessful.</returns>
        private MsiPayload GetCachedMsiPayload(string packageId, string packageVersion, DirectoryPath? offlineCache)
        {
            if (!Cache.TryGetPayloadFromCache(packageId, packageVersion, out MsiPayload msiPayload))
            {
                // If it's not fully cached, download or copy if from the local cache and extract the payload package into a
                // temporary location and try to cache it again in the MSI cache. We DO NOT trust partially cached packages.
                string extractedPackageRootPath = ExtractPackage(packageId, packageVersion, offlineCache);
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
        /// Executes an MSI package. The type of execution is determined by the requested action.
        /// </summary>
        /// <param name="msi">The MSI package to execute.</param>
        /// <param name="action">The action to perform.</param>
        private void ExecutePackage(MsiPayload msi, InstallAction action)
        {
            uint error = Error.SUCCESS;
            string logFile = GetMsiLogName(msi, action);

            switch (action)
            {
                case InstallAction.MinorUpdate:
                case InstallAction.Install:
                case InstallAction.MajorUpgrade:
                    error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressInstall, msi.Payload), () => InstallMsi(msi.MsiPath, logFile));
                    ExitOnError(error, $"Failed to install {msi.Payload}.");
                    break;

                case InstallAction.Repair:
                    error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressRepair, msi.Payload), () => RepairMsi(msi.ProductCode, logFile));
                    ExitOnError(error, $"Failed to repair {msi.Payload}.");
                    break;

                case InstallAction.Uninstall:
                    error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressUninstall, msi.Payload), () => UninstallMsi(msi.ProductCode, logFile));
                    ExitOnError(error, $"Failed to remove {msi.Payload}.");
                    break;

                case InstallAction.None:
                case InstallAction.Downgrade:
                default:
                    break;
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
            bool verifySignatures,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null,
            string tempDirPath = null,
            RestoreActionConfig restoreActionConfig = null)
        {
            TimestampedFileLogger logger = new(Path.Combine(Path.GetTempPath(), $"Microsoft.NET.Workload_{DateTime.Now:yyyyMMdd_HHmmss}.log"));
            InstallClientElevationContext elevationContext = new(logger);

            if (nugetPackageDownloader == null)
            {
                DirectoryPath tempPackagesDir = new(string.IsNullOrWhiteSpace(tempDirPath) ? Path.GetTempPath() : tempDirPath);

                nugetPackageDownloader = new NuGetPackageDownloader(tempPackagesDir,
                    filePermissionSetter: null, new FirstPartyNuGetPackageSigningVerifier(),
                    new NullLogger(), restoreActionConfig: restoreActionConfig);
            }

            return new NetSdkMsiInstallerClient(elevationContext, logger, verifySignatures, workloadResolver, sdkFeatureBand, nugetPackageDownloader,
                verbosity, packageSourceLocation, reporter);
        }

        /// <summary>
        /// Reports any pending reboots.
        /// </summary>
        private void ReportPendingReboot()
        {
            if (RebootPending)
            {
                ReportOnce(AnsiColorExtensions.Yellow(LocalizableStrings.PendingReboot));
            }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (!_shutdown)
            {
                try
                {
                    Shutdown();
                }
                catch (Exception ex)
                {
                    // Don't rethrow. We'll call ShutDown during abnormal termination when control is passing back to the host
                    // so there's nothing in the CLI that will catch the exception.
                    Log?.LogMessage($"OnProcessExit: Shutdown failed, {ex.Message}");
                }
                finally
                {
                    ((TimestampedFileLogger)Log).Dispose();
                }
            }
        }
    }
}
