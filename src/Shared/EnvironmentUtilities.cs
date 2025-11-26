// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Build.Shared
{
    internal static partial class EnvironmentUtilities
    {
#if NET472_OR_GREATER || NETCOREAPP
        public static bool Is64BitProcess => Marshal.SizeOf<IntPtr>() == 8;

        public static bool Is64BitOperatingSystem =>
            Environment.Is64BitOperatingSystem;
#endif

#if !NETCOREAPP
        private static volatile int s_processId;
        private static volatile string? s_processPath;
#endif
        private static volatile string? s_processName;

        /// <summary>
        /// Gets the string comparer for environment variable names based on the current platform.
        /// On Windows, environment variables are case-insensitive; on Unix-like systems, they are case-sensitive.
        /// </summary>
        internal static StringComparer EnvironmentVariableComparer =>
            NativeMethodsShared.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        /// <summary>Gets the unique identifier for the current process.</summary>
        public static int CurrentProcessId
        {
            get
            {
#if NETCOREAPP
                return Environment.ProcessId;
#else
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
#endif
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
#if NETCOREAPP
                return Environment.ProcessPath;
#else
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
#endif
            }
        }

        public static string ProcessName
        {
            get
            {
                string? processName = s_processName;
                if (processName == null)
                {
                    using Process currentProcess = Process.GetCurrentProcess();
                    Interlocked.CompareExchange(ref s_processName, currentProcess.ProcessName, null);
                    processName = s_processName;
                }

                return processName;
            }
        }

        public static bool IsWellKnownEnvironmentDerivedProperty(string propertyName)
        {
            return propertyName.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                propertyName.StartsWith("COMPLUS_", StringComparison.OrdinalIgnoreCase) ||
                propertyName.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Copies environment variables from the current process into a new dictionary.
        /// </summary>
        /// <returns>A dictionary containing all current process environment variables.</returns>
        internal static Dictionary<string, string> CopyCurrentEnvironmentVariables()
        {
            IDictionary variables = Environment.GetEnvironmentVariables();
            var result = new Dictionary<string, string>(variables.Count, EnvironmentVariableComparer);

            foreach (string key in variables.Keys)
            {
                if (variables[key] is string value)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Updates the target environment to match the provided dictionary by removing variables
        /// that are no longer present and updating variables that have changed.
        /// </summary>
        /// <param name="newEnvironment">The desired environment state.</param>
        /// <param name="getCurrentEnvironment">Function to get the current environment state.</param>
        /// <param name="setVariable">Action to set or remove (when value is null) an environment variable.</param>
        internal static void SetEnvironment(
            IDictionary<string, string> newEnvironment,
            Func<IReadOnlyDictionary<string, string>> getCurrentEnvironment,
            Action<string, string?> setVariable)
        {
            if (newEnvironment == null)
            {
                return;
            }

            IReadOnlyDictionary<string, string> currentEnvironment = getCurrentEnvironment();

            // First, delete all no longer set variables
            foreach (KeyValuePair<string, string> entry in currentEnvironment)
            {
                if (!newEnvironment.ContainsKey(entry.Key))
                {
                    setVariable(entry.Key, null);
                }
            }

            // Then, make sure the new ones have their new values.
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                if (!currentEnvironment.TryGetValue(entry.Key, out string? currentValue) || currentValue != entry.Value)
                {
                    setVariable(entry.Key, entry.Value);
                }
            }
        }
    }
}
