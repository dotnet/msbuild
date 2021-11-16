// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// Extensions methods for <see cref="Process"/> components.
    /// </summary>
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public static class ProcessExtensions
    {
#pragma warning disable CA1416
        /// <summary>
        /// Returns the parent process of this process by querying the Win32_Process class.
        /// </summary>
        /// <param name="process">The process component.</param>
        /// <returns>The parent process or <see langword="null"/> if the parent process cannot be found.</returns>
        public static Process GetParentProcess(this Process process)
        {
            int ppid = process.GetParentProcessId();

            return ppid != -1 ? Process.GetProcessById(ppid) : null;
        }

        /// <summary>
        /// Returns the parent process ID of this process by querying the Win32_Process class.
        /// </summary>
        /// <param name="process">The process component.</param>
        /// <returns>The process ID of the parent process, or -1 if the parent process could not be found.</returns>
        public unsafe static int GetParentProcessId(this Process process)
        {
            SafeProcessHandle handle = process.SafeHandle;
            NativeMethods.Windows.PROCESS_BASIC_INFORMATION info;

            if (NativeMethods.Windows.NtQueryInformationProcess(handle, NativeMethods.Windows.ProcessBasicInformation,
                &info, (uint)sizeof(NativeMethods.Windows.PROCESS_BASIC_INFORMATION), out _) != 0)
            {
                return -1;
            }

            return (int)info.InheritedFromUniqueProcessId;
        }
#pragma warning restore CA1416
    }
}
