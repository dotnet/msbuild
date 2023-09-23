// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;
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
        /// Track messages that should never be reported more than once.
        /// </summary>
        private HashSet<string> _reportedMessages = new();

        /// <summary>
        /// Backing field for the install location of .NET
        /// </summary>
        private string _dotNetHome;

        /// <summary>
        /// Default reinstall mode (equivalent to VOMUS).
        /// </summary>
        public const ReinstallMode DefaultReinstallMode = ReinstallMode.FILEOLDERVERSION | ReinstallMode.FILEVERIFY |
            ReinstallMode.MACHINEDATA | ReinstallMode.USERDATA | ReinstallMode.SHORTCUT | ReinstallMode.PACKAGE;

        /// <summary>
        /// The prefix used when registering a dependent against a provider key.
        /// </summary>
        protected const string DependentPrefix = "Microsoft.NET.Sdk";

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

        /// <summary>
        /// The install location of the .NET based on the host and OS architecture as stored in the registry. If
        /// no registry entry exists, the default location is returned.
        /// </summary>
        protected string DotNetHome
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_dotNetHome))
                {
                    _dotNetHome = GetDotNetHome();
                }

                return _dotNetHome;
            }
        }

        protected readonly IReporter Reporter;

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
            bool verifySignatures, IReporter reporter = null) : base(elevationContext, logger, verifySignatures)
        {
            Cache = new MsiPackageCache(elevationContext, logger, verifySignatures);
            RecordRepository = new RegistryWorkloadInstallationRecordRepository(elevationContext, logger, VerifySignatures);
            UpdateAgent = new WindowsUpdateAgent(logger);
            Reporter = reporter;
        }

        /// <summary>
        /// Detect installed workload pack records. Only the default registry hive is searched. Finding a workload pack
        /// record does not necessarily guarantee that the MSI is installed.
        /// </summary>
        protected List<WorkloadPackRecord> GetWorkloadPackRecords()
        {
            Log?.LogMessage($"Detecting installed workload packs for {HostArchitecture}.");
            List<WorkloadPackRecord> workloadPackRecords = new();
            using RegistryKey installedPacksKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPacks\{HostArchitecture}");

            static void SetRecordMsiProperties(WorkloadPackRecord record, RegistryKey key)
            {
                record.ProviderKeyName = (string)key.GetValue("DependencyProviderKey");
                record.ProductCode = (string)key.GetValue("ProductCode");
                record.ProductVersion = new Version((string)key.GetValue("ProductVersion"));
                record.UpgradeCode = (string)key.GetValue("UpgradeCode");
            }

            if (installedPacksKey != null)
            {
                foreach (string packId in installedPacksKey.GetSubKeyNames())
                {
                    using RegistryKey packKey = installedPacksKey.OpenSubKey(packId);

                    foreach (string packVersion in packKey.GetSubKeyNames())
                    {
                        using RegistryKey packVersionKey = packKey.OpenSubKey(packVersion);

                        WorkloadPackRecord record = new()
                        {
                            MsiId = packId,
                            MsiNuGetVersion = packVersion,
                        };

                        SetRecordMsiProperties(record, packVersionKey);

                        record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));

                        Log?.LogMessage($"Found workload pack record, Id: {packId}, version: {packVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        workloadPackRecords.Add(record);
                    }
                }
            }

            //  Workload pack group installation records are in a similar format as the pack installation records.  They use the "InstalledPackGroups" key,
            //  and under the key for each pack group/version are keys for the workload pack IDs and versions that are in the pack gorup.
            using RegistryKey installedPackGroupsKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\{HostArchitecture}");
            if (installedPackGroupsKey != null)
            {
                foreach (string packGroupId in installedPackGroupsKey.GetSubKeyNames())
                {
                    using RegistryKey packGroupKey = installedPackGroupsKey.OpenSubKey(packGroupId);
                    foreach (string packGroupVersion in packGroupKey.GetSubKeyNames())
                    {
                        using RegistryKey packGroupVersionKey = packGroupKey.OpenSubKey(packGroupVersion);

                        WorkloadPackRecord record = new()
                        {
                            MsiId = packGroupId,
                            MsiNuGetVersion = packGroupVersion
                        };

                        SetRecordMsiProperties(record, packGroupVersionKey);

                        Log?.LogMessage($"Found workload pack group record, Id: {packGroupId}, version: {packGroupVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        foreach (string packId in packGroupVersionKey.GetSubKeyNames())
                        {
                            using RegistryKey packIdKey = packGroupVersionKey.OpenSubKey(packId);
                            foreach (string packVersion in packIdKey.GetSubKeyNames())
                            {
                                record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));
                                Log?.LogMessage($"Found workload pack in group, Id: {packId}, version: {packVersion}");
                            }
                        }

                        workloadPackRecords.Add(record);
                    }
                }
            }

            return workloadPackRecords;
        }

        /// <summary>
        /// Determines the per-machine install location for .NET. This is similar to the logic in the standalone installers.
        /// </summary>
        /// <returns>The path where .NET is installed based on the host architecture and operating system bitness.</returns>
        internal static string GetDotNetHome()
        {
            // Configure the default location, e.g., if the registry key is absent. Technically that would be suggesting
            // that the install is corrupt or we're being asked to run as an admin install in a non-admin deployment.
            Environment.SpecialFolder programFiles = string.Equals(HostArchitecture, "x86") && Environment.Is64BitOperatingSystem
                ? Environment.SpecialFolder.ProgramFilesX86
                : Environment.SpecialFolder.ProgramFiles;
            string dotNetHome = Path.Combine(Environment.GetFolderPath(programFiles), "dotnet");

            using RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            if (hklm != null)
            {
                using RegistryKey hostKey = hklm.OpenSubKey($@"SOFTWARE\dotnet\Setup\InstalledVersions\{HostArchitecture}");

                if (hostKey != null)
                {
                    string installLocation = (string)hostKey.GetValue("InstallLocation");

                    if (!string.IsNullOrWhiteSpace(installLocation))
                    {
                        return installLocation;
                    }
                }
            }

            return dotNetHome;
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
                string installProperties = InstallProperties.Create(InstallProperties.SystemComponent,
                    InstallProperties.FastInstall, InstallProperties.SuppressReboot,
                    $@"DOTNETHOME=""{DotNetHome}""");
                return WindowsInstaller.InstallProduct(packagePath, installProperties);
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
        /// <param name="ignoreDependencies">Controls whether dependency checks should be ignored when uninstalling.</param>
        /// <returns>An error code indicating the result of the operation.</returns>
        protected uint UninstallMsi(string productCode, string logFile, bool ignoreDependencies = false)
        {
            Elevate();

            if (IsElevated)
            {
                ConfigureInstall(logFile);
                string installProperties = InstallProperties.Create(InstallProperties.SystemComponent,
                    InstallProperties.FastInstall, InstallProperties.SuppressReboot,
                    InstallProperties.RemoveAll,
                    ignoreDependencies ? InstallProperties.IgnoreDependencies : null);
                return WindowsInstaller.ConfigureProduct(productCode, WindowsInstaller.INSTALLLEVEL_DEFAULT, InstallState.ABSENT,
                    installProperties);
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
        /// Creates the log filename to use when executing an MSI. The name is based on the primary log, workload pack and <see cref="InstallAction"/>.
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
        /// Creates the log filename to use when executing an MSI. The name is based on the primary log, payload name and <see cref="InstallAction"/>.
        /// </summary>
        /// <param name="packInfo">The workload pack to use when generating the log name.</param>
        /// <param name="action">The install action that will be performed.</param>
        /// <returns>The full path of the log file.</returns>
        protected string GetMsiLogName(MsiPayload msi, InstallAction action)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{msi.Manifest.Payload}_{action}.log");
        }

        /// <summary>
        /// Creates the log filename to use when executing an MSI. The name is based on the primary log, ProductCode and <see cref="InstallAction"/>.
        /// </summary>
        /// <param name="packInfo">The workload pack to use when generating the log name.</param>
        /// <param name="action">The install action that will be performed.</param>
        /// <returns>The full path of the log file.</returns>
        protected string GetMsiLogName(string productCode, InstallAction action)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{productCode}_{action}.log");
        }

        /// <summary>
        /// Creates the log filename to use when performing an admin install on an MSI.
        /// </summary>
        /// <param name="msiPath">The full path to the MSI</param>
        /// <returns>The full path of the log file</returns>
        protected string GetMsiLogNameForAdminInstall(string msiPath)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{Path.GetFileNameWithoutExtension(msiPath)}_AdminInstall.log");
        }

        /// <summary>
        /// Creates the log filename to use when executing an MSI. The name is based on the primary log, workload pack record and <see cref="InstallAction"/>.
        /// </summary>
        /// <param name="record">The workload record to use when generating the log name.</param>
        /// <param name="action">The install action that will be performed.</param>
        /// <returns>The full path of the log file.</returns>
        protected string GetMsiLogName(WorkloadPackRecord record, InstallAction action)
        {
            return Path.Combine(Path.GetDirectoryName(Log.LogPath),
                Path.GetFileNameWithoutExtension(Log.LogPath) + $"_{record.MsiId}-{record.MsiNuGetVersion}_{action}.log");
        }

        /// <summary>
        /// Get a list of all MSI based SDK installations that match the current host architecture.
        /// </summary>
        /// <returns>A collection of all the installed SDKs. The collection may be empty if no installed versions are found.</returns>
        internal static IEnumerable<string> GetInstalledSdkVersions()
        {
            // The SDK, regardless of the installer's platform, writes detection keys to the 32-bit hive.
            using RegistryKey hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            if (hklm32 == null)
            {
                return Enumerable.Empty<string>();
            }

            using RegistryKey installedSdkVersionsKey = hklm32.OpenSubKey(@$"SOFTWARE\dotnet\Setup\InstalledVersions\{HostArchitecture}\sdk");

            // Call ToList() since the registry key handle will be disposed when exiting and deferred execution will fail.
            return installedSdkVersionsKey?.GetValueNames().Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Writes a messages to the underlying <see cref="IReporter"/> if the message has not previously been reported.
        /// </summary>
        /// <param name="message">The message to report.</param>
        protected void ReportOnce(string message)
        {
            if (!_reportedMessages.Contains(message))
            {
                Reporter.WriteLine(message);
                _reportedMessages.Add(message);
            }
        }

        /// <summary>
        /// Updates a dependency provider key by adding or removing a dependent.
        /// </summary>
        /// <param name="requestType">The action to perform on the provider key.</param>
        /// <param name="providerKeyName">The provider key to update.</param>
        /// <param name="dependent">The dependent to add or remove.</param>
        protected void UpdateDependent(InstallRequestType requestType, string providerKeyName, string dependent)
        {
            DependencyProvider provider = new(providerKeyName, allUsers: true);

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
                    // NB: Do not remove the provider key. The dependency provider custom action in the MSI will fail
                    // if it cannot find the key.
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
                InstallResponseMessage response = Dispatcher.SendDependentRequest(requestType, providerKeyName, dependent);
                ExitOnFailure(response, $"Failed to update dependent, providerKey: {providerKeyName}, dependent: {dependent}.");
            }
        }
    }
}
