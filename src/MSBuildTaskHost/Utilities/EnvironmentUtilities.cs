// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.TaskHost.Utilities
{
    internal static partial class EnvironmentUtilities
    {
        private static volatile int s_processId;
        private static volatile string? s_processPath;

        /// <summary>Gets the unique identifier for the current process.</summary>
        public static int CurrentProcessId
        {
            get
            {
                // copied from Environment.ProcessId
                int processId = s_processId;
                if (processId == 0)
                {
                    using Process currentProcess = Process.GetCurrentProcess();
                    s_processId = processId = currentProcess.Id;

                    // Assume that process Id zero is invalid for user processes. It holds for all mainstream operating systems.
                    Debug.Assert(processId != 0);
                }

                return processId;
            }
        }

        /// <summary>
        /// Returns the path of the executable that started the currently executing process. Returns null when the path is not available.
        /// </summary>
        /// <returns>Path of the executable that started the currently executing process</returns>
        /// <remarks>
        /// If the executable is renamed or deleted before this property is first accessed, the return value is undefined and depends on the operating system.
        /// </remarks>
        public static string? ProcessPath
        {
            get
            {
                // copied from Environment.ProcessPath
                string? processPath = s_processPath;
                if (processPath == null)
                {
                    // The value is cached both as a performance optimization and to ensure that the API always returns
                    // the same path in a given process.
                    using Process currentProcess = Process.GetCurrentProcess();
                    Interlocked.CompareExchange(ref s_processPath, currentProcess.MainModule.FileName ?? "", null);
                    processPath = s_processPath;
                    Debug.Assert(processPath != null);
                }

                return (processPath?.Length != 0) ? processPath : null;
            }
        }
    }
}
