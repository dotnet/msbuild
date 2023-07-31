// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Win32;

namespace Microsoft.DotNet.Installer.Windows
{
    [SupportedOSPlatform("windows")]
    /// <summary>
    /// Utility methods, specific to Windows.
    /// </summary>
    public static class WindowsUtils
    {
        /// <summary>
        /// Generate a pseudo-random pipe name using the specified process ID, hashed MAC address and process path.
        /// </summary>
        /// <param name="processId">The process ID to use for generating the pipe name.</param>
        /// <param name="values">Additional values to incorporate into the generated name.</param>
        /// <returns>A string containing the pipe name.</returns>
        public static string CreatePipeName(int processId, params string[] values)
        {
            // Reinvoking the host can cause differences between the original path, e.g.,
            // "C:\Program Files" and "c:\Program Files". This will generate different UUID values and cause
            // deadlock when the client and server are trying to connect, so always use the lower invariant of the process.
            return Uuid.Create($"{processId};{Environment.ProcessPath.ToLowerInvariant()};{Sha256Hasher.Hash(MacAddressGetter.GetMacAddress())};{string.Join(";", values)}")
                .ToString("B");
        }

        /// <summary>
        /// Determines whether the current user has the Administrator role.
        /// </summary>
        /// <returns><see langword="true"/> if the user has the Administrator role.</returns>
        public static bool IsAdministrator()
        {
            WindowsPrincipal principal = new(WindowsIdentity.GetCurrent());

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Determine if an install is running by trying to open the global _MSIExecute mutex. The mutex is
        /// only set while processing the InstallExecuteSequence, AdminExecuteSequence or AdvtExecuteSequence tables.
        /// </summary>
        /// <returns><see langword="true" /> if another install is already running; <see langword="false"/> otherwise.</returns>
        /// See the <see href="https://docs.microsoft.com/en-us/windows/win32/msi/-msiexecute-mutex">_MSIMutex</see> documentation.
        public static bool InstallRunning()
        {
            return !Mutex.TryOpenExisting(@"Global\_MSIExecute", out _);
        }

        /// <summary>
        /// Queries the Windows Update Agent, Component Based Servicing (CBS), and pending file rename registry keys to determine if there is a pending reboot.
        /// </summary>
        /// <returns><see langword="true"/> if there is a pending reboot; <see langword="false"> otherwise.</see></returns>
        public static bool RebootRequired()
        {
            using RegistryKey localMachineKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey auKey = localMachineKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using RegistryKey cbsKey = localMachineKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using RegistryKey sessionKey = localMachineKey?.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");

            string[] pendingFileRenameOperations = (string[])sessionKey?.GetValue("PendingFileRenameOperations") ?? new string[0];

            return (auKey != null || cbsKey != null || pendingFileRenameOperations.Length > 0);
        }
    }
}
