// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.Principal;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// Utility methods, specific to Windows.
    /// </summary>
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public static class Windows
    {
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
        /// Returns the commandline of the currently executing process.
        /// </summary>
        /// <returns>The commandline of the current process.</returns>
        public static string GetProcessCommandLine()
        {
            return Marshal.PtrToStringAuto(NativeMethods.Windows.GetCommandLine());
        }
    }
}
