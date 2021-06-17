// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;

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
        /// Queries the Windows Update Agent API to determine if there is a pending reboot.
        /// </summary>
        /// <returns><see langword="true"/> if there is a pending reboot; <see langword="false"> otherwise.</see></returns>
        /// <remarks>
        /// See <see href="https://docs.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-isysteminformation-get_rebootrequired">this</see>
        /// for more information.
        /// </remarks>
        public static bool RebootRequired()
        {
            return new SystemInformationClass().RebootRequired;
        }
    }
}
