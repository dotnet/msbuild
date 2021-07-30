// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Management;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// Extensions methods for <see cref="Process"/> components.
    /// </summary>
    public static class ProcessExtensions
    {
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
        public static int GetParentProcessId(this Process process)
        {
            ManagementObjectSearcher searcher = new($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId='{process.Id}'");
            using ManagementObjectCollection result = searcher.Get();
            ManagementObjectCollection.ManagementObjectEnumerator enumerator = result.GetEnumerator();

            return enumerator.MoveNext() ? Convert.ToInt32(enumerator.Current.GetPropertyValue("ParentProcessId")) : -1;
        }

        /// <summary>
        /// Returns the command line of this process by querying the Win32_Process class.
        /// </summary>
        /// <param name="process">The process component.</param>
        /// <returns>The command line of the process or <see langword="null"/> if it could not be retrieved.</returns>
        public static string GetCommandLine(this Process process)
        {
            ManagementObjectSearcher searcher = new($"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{process.Id}'");
            using ManagementObjectCollection result = searcher.Get();
            ManagementObjectCollection.ManagementObjectEnumerator enumerator = result.GetEnumerator();

            return enumerator.MoveNext() ? Convert.ToString(enumerator.Current.GetPropertyValue("CommandLine")) : null;
        }
    }
}
