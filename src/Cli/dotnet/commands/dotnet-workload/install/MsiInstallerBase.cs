// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Win32;
using Microsoft.Win32.Msi;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Installer.Windows
{
    [SupportedOSPlatform("windows")]
    internal abstract class MsiInstallerBase : InstallerBase
    {
        /// <summary>
        /// Default reinstall mode (equivalent to VOMUS).
        /// </summary>
        public const ReinstallMode DefaultReinstallMode = ReinstallMode.FILEOLDERVERSION | ReinstallMode.FILEVERIFY |
            ReinstallMode.MACHINEDATA | ReinstallMode.USERDATA | ReinstallMode.SHORTCUT | ReinstallMode.PACKAGE;

        /// <summary>
        /// The prefix used when registering a dependent against a provider key.
        /// </summary>
        protected const string DependentPrefix = "Microsoft.NET.Sdk";

        // Set MSIFASTINSTALL=7 and be explicit about what this means.
        private const int _msiFastInstall = (int)(MsiFastInstall.NoSystemRestore | MsiFastInstall.OnlyFileCosting | MsiFastInstall.ReducedProgressFrequency);

        /// <summary>
        /// Set of default properties to use when installing.
        /// </summary>
        private static readonly string s_installProperties = $"MSIFASTINSTALL={_msiFastInstall} ARPSYSTEMCOMPONENT=1 REBOOT=ReallySuppress";

        /// <summary>
        /// Set of default properties to use when repairing.
        /// </summary>
        private static readonly string s_repairProperties = $"MSIFASTINSTALL={_msiFastInstall} ARPSYSTEMCOMPONENT=1 REBOOT=ReallySuppress REINSTALLMODE=vomus REINSTALL=ALL";

        /// <summary>
        /// Set of default properties to use when uninstalling.
        /// </summary>
        private static readonly string s_uninstallProperties = $"{s_installProperties} REMOVE=ALL";

        /// <summary>
        /// Supported installer architectures used to map workload packs to architecture
        /// specific payload packs to acquire MSIs.
        /// </summary>
        protected static readonly string[] SupportedArchitectures = { "x86", "amd64", "arm64" };

        /// <summary>
        /// Determines whether the parent process is still active.
        /// </summary>
        protected bool IsParentProcessRunning => Process.GetProcessById(ParentProcess.Id) != null;

        /// <summary>
        /// Provides access to the underlying MSI cache.
        /// </summary>
        protected MsiPackageCache Cache
        {
            get;
            private set;
        }

        protected readonly IReporter Reporter;

        /// <summary>
        /// Gets the set of currently installed workload pack records by querying the registry.
        /// </summary>
        protected IReadOnlyDictionary<string, List<WorkloadPackRecord>> WorkloadPackRecords => GetWorkloadPackRecords();

        /// <summary>
        /// A service controller representing the Windows Update agent (wuaserv).
        /// </summary>
        protected readonly WindowsUpdateAgent UpdateAgent;

        /// <summary>
        /// Provides access to workload installation records in the registry.
        /// </summary>
        protected readonly RegistryWorkloadInstallationRecordRepository RecordRepository;

        /// <summary>
        /// Creates a new <see cref="MsiInstallerBase"/> instance.
        /// </summary>
        /// <param name="dispatcher">The command dispatcher used for sending and receiving commands.</param>
        /// <param name="logger"></param>
        /// <param name="reporter"></param>
        public MsiInstallerBase(InstallElevationContextBase elevationContext, ISetupLogger logger,
            IReporter reporter = null) : base(elevationContext, logger)
        {
            Cache = new MsiPackageCache(elevationContext, logger);
            RecordRepository = new RegistryWorkloadInstallationRecordRepository(elevationContext, logger);
            UpdateAgent = new WindowsUpdateAgent(logger);
            Reporter = reporter;
        }

        /// <summary>
        /// Detect installed workload pack records. Only the default registry hive is searched. Finding a workload pack
        /// record does not necessarily guarantee that the MSI is installed.
        /// </summary>
        private IReadOnlyDictionary<string, List<WorkloadPackRecord>> GetWorkloadPackRecords()
        {
            Log?.LogMessage("Detecting installed workload packs.");
            Dictionary<string, List<WorkloadPackRecord>> workloadPackRecords = new Dictionary<string, List<WorkloadPackRecord>>();
            using RegistryKey installedPacksKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\dotnet\InstalledPacks");

            foreach (string packId in installedPacksKey.GetSubKeyNames())
            {
                if (!workloadPackRecords.ContainsKey(packId))
                {
                    workloadPackRecords[packId] = new List<WorkloadPackRecord>();
                }

                using RegistryKey packKey = installedPacksKey.OpenSubKey(packId);

                foreach (string packVersion in packKey.GetSubKeyNames())
                {
                    using RegistryKey packVersionKey = packKey.OpenSubKey(packVersion);

                    WorkloadPackRecord record = new WorkloadPackRecord
                    {
                        ProviderKeyName = (string)packVersionKey.GetValue("DependencyProviderKey"),
                        PackId = new NET.Sdk.WorkloadManifestReader.WorkloadPackId(packId),
                        PackVersion = new NuGetVersion(packVersion),
                        ProductCode = (string)packVersionKey.GetValue("ProductCode"),
                        ProductVersion = new Version((string)packVersionKey.GetValue("ProductVersion"))
                    };

                    Log?.LogMessage($"Found workload pack record, Id: {record.PackId}, version: {record.PackVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                    workloadPackRecords[packId].Add(record);
                }
            }

            return new ReadOnlyDictionary<string, List<WorkloadPackRecord>>(workloadPackRecords);
        }

        /// <summary>
        /// Configures the installer UI and logging operations before starting an operation.
        /// </summary>
        /// <param name="logFile">The path of the log file.</param>
        protected void ConfigureInstall(string logFile)
        {
            uint error = Error.SUCCESS;

            // Turn off the MSI UI.
            _ = WindowsInstaller.SetInternalUI(InstallUILevel.None);

            // The log file must be created before calling MsiEnableLog and we should avoid having active handles
            // against it.
            FileStream logFileStream = File.Create(logFile);
            logFileStream.Close();
            error = WindowsInstaller.EnableLog(InstallLogMode.DEFAULT | InstallLogMode.VERBOSE, logFile, InstallLogAttributes.NONE);

            // We can report issues with the log file creation, but shouldn't fail the workload operation.
            LogError(error, $"Failed to configure log file: {logFile}");
        }

        /// <summary>
        /// Repairs the specified MSI package.
        /// </summary>
        /// <param name="productCode">The product code of the MSI to repair.</param>
        /// <param name="logFile">The full path of the log file.</param>
        /// <returns>An error code indicating the result of the operation.</returns>
        protected uint RepairMsi(string productCode, string logFile)
        {
            Elevate();

            if (IsElevated)
            {
                ConfigureInstall(logFile);
                return WindowsInstaller.ReinstallProduct(productCode, DefaultReinstallMode);
            }
            else if (IsClient)
            {
                InstallResponseMessage response = Dispatcher.SendMsiRequest(InstallRequestType.RepairMsi,
                    logFile, null, productCode);
                ExitOnFailure(response, "Failed to repair MSI.");

                return response.Error;
            }

            throw new InvalidOperationException($"Invalid configuration: elevated: {IsElevated}, client: {IsClient}");
        }

        /// <summary>
        /// Installs the specified MSI.
        /// </summary>
        /// <param name="packagePath">The full path to the MSI package.</param>
        /// <param name="logFile">The full path of the log file.</param>
        /// <returns>An error code indicating the result of the operation.</returns>
        protected uint InstallMsi(string packagePath, string logFile)
        {
            // Make sure the package we're going to run is coming from the cache.
            if (!packagePath.StartsWith(Cache.PackageCacheRoot))
            {
                return Error.INSTALL_PACKAGE_INVALID;
            }

            Elevate();

            if (IsElevated)
            {
                ConfigureInstall(logFile);
                return WindowsInstaller.InstallProduct(packagePath, s_installProperties);
            }
            else if (IsClient)
            {
                InstallResponseMessage response = Dispatcher.SendMsiRequest(InstallRequestType.InstallMsi,
                    logFile, packagePath);
                ExitOnFailure(response, "Failed to install MSI.");

                return response.Error;
            }

            throw new InvalidOperationException($"Invalid configuration: elevated: {IsElevated}, client: {IsClient}");
        }

        /// <summary>
        /// Uninstalls the MSI using its provided product code.
        /// </summary>
        /// <param name="productCode">The product code of the MSI to uninstall.</param>
        /// <param name="logFile">The full path of the log file.</param>
        /// <returns>An error code indicating the result of the operation.</returns>
        protected uint UninstallMsi(string productCode, string logFile)
        {
            Elevate();

            if (IsElevated)
            {
                ConfigureInstall(logFile);
                return WindowsInstaller.ConfigureProduct(productCode, WindowsInstaller.INSTALLLEVEL_DEFAULT, InstallState.ABSENT,
                    s_uninstallProperties);
            }
            else if (IsClient)
            {
                InstallResponseMessage response = Dispatcher.SendMsiRequest(InstallRequestType.UninstallMsi,
                    logFile, null, productCode);
                ExitOnFailure(response, "Failed to uninstall MSI.");

                return response.Error;
            }

            throw new InvalidOperationException($"Invalid configuration: elevated: {IsElevated}, client: {IsClient}");
        }

        /// <summary>
        /// Moves a file from one location to another if the destination file does not already exist.
        /// </summary>
        /// <param name="sourceFile">The source file to move.</param>
        /// <param name="destinationFile">The destination where the source file will be moved.</param>
        protected void MoveFile(string sourceFile, string destinationFile)
        {
            if (!File.Exists(destinationFile))
            {
                FileAccessRetrier.RetryOnMoveAccessFailure(() => File.Move(sourceFile, destinationFile));
                Log?.LogMessage($"Moved '{sourceFile}' to '{destinationFile}'");
            }
        }

        /// <summary>
        /// Creates the log filename to use when executing an MSI. The name is based on the primar log, workload pack and <see cref="InstallAction"/>.
        /// </summary>
        /// <param name="packInfo">The workload pack to use when generating the log name.</param>
        /// <param name="action">The install action that will be performed.</param>
        /// <returns>The full path of the log file.</returns>
        protected string GetMsiLogName(PackInfo packInfo, InstallAction action)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{packInfo.ResolvedPackageId}-{packInfo.Version}_{action}.log");
        }

        /// <summary>
        /// Creates the log filename to use when executing an MSI. The name is based on the primar log, workload pack and <see cref="InstallAction"/>.
        /// </summary>
        /// <param name="record">The workload record to use when generating the log name.</param>
        /// <param name="action">The install action that will be performed.</param>
        /// <returns>The full path of the log file.</returns>
        protected string GetMsiLogName(WorkloadPackRecord record, InstallAction action)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{record.PackId}-{record.PackVersion}_{action}.log");
        }

        /// <summary>
        /// Get a list of all MSI based SDK installations that match the current host architecture.
        /// </summary>
        /// <returns>A collection of all the installed SDKs. The collection may be empty if no installed versions are found.</returns>
        protected IEnumerable<string> GetInstalledSdkVersions()
        {
            // The SDK, regardless of the installer's platform, writes detection keys to the 32-bit hive.
            using RegistryKey hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            if (hklm32 == null)
            {
                return Enumerable.Empty<string>();
            }

            using RegistryKey installedSdkVersionsKey = hklm32.OpenSubKey(@$"SOFTWARE\dotnet\Setup\InstalledVersions\{HostArchitecture}\sdk");

            // Call ToList() since the registry key handle will be disposed when exiting and deferred execution will fail.
            return installedSdkVersionsKey?.GetValueNames().ToList() ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Updates a dependency provider key by adding or removing a dependent.
        /// </summary>
        /// <param name="requestType">The action to perform on the provider key.</param>
        /// <param name="providerKeyName">The provider key to update.</param>
        /// <param name="dependent">The dependent to add or remove.</param>
        protected void UpdateDependent(InstallRequestType requestType, string providerKeyName, string dependent)
        {
            DependencyProvider provider = new DependencyProvider(providerKeyName, allUsers: true);

            if (provider.Dependents.Contains(dependent) && requestType == InstallRequestType.AddDependent)
            {
                Log?.LogMessage($"Dependent already exists, {providerKeyName} won't be modified.");
                return;
            }
                
            if (!provider.Dependents.Contains(dependent) && requestType == InstallRequestType.RemoveDependent)
            {
                Log?.LogMessage($"Dependent doesn't exist, {providerKeyName} won't be modified.");
                return;
            }

            Elevate();

            if (IsElevated)
            {
                if (requestType == InstallRequestType.RemoveDependent)
                {
                    Log?.LogMessage($"Removing dependent '{dependent}' from provider '{providerKeyName}'");
                    provider.RemoveDependent(dependent, removeProvider: false);
                }
                else if (requestType == InstallRequestType.AddDependent)
                {
                    Log?.LogMessage($"Registering dependent '{dependent}' on provider '{providerKeyName}'");
                    provider.AddDependent(dependent);
                }
            }
            else if (IsClient)
            {
                var response = Dispatcher.SendDependentRequest(requestType, providerKeyName, dependent);
            }
        }
    }
}
