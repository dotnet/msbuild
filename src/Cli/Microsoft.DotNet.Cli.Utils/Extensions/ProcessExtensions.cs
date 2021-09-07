// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Management.Infrastructure;

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
        public static int GetParentProcessId(this Process process)
        {
            CimSession cimSession = CimSession.Create(null);
            IEnumerable<CimInstance> results = cimSession.QueryInstances(@"root\cimv2", "WQL",
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId='{process.Id}'");

            return results.Any() ? Convert.ToInt32(results.First().CimInstanceProperties["ParentProcessId"].Value) : -1;
        }

        /// <summary>
        /// Returns the command line of this process by querying the Win32_Process class.
        /// </summary>
        /// <param name="process">The process component.</param>
        /// <returns>The command line of the process or <see langword="null"/> if it could not be retrieved.</returns>
        public static string GetCommandLine(this Process process)
        {
            CimSession cimSession = CimSession.Create(null);
            IEnumerable<CimInstance> results = cimSession.QueryInstances(@"root\cimv2", "WQL",
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{process.Id}'");

            return results.Any() ? Convert.ToString(results.First().CimInstanceProperties["CommandLine"].Value) : null;
        }
#pragma warning restore CA1416
    }
}
