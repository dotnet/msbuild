// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif
#if NET
using System.Security.Principal;
#endif
using Microsoft.Build.Internal;
#if FEATURE_WINDOWSINTEROP && NET
using Windows.Win32;
using Windows.Win32.Foundation;
#endif

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Implementation of <see cref="ITaskEnvironmentDriver"/> that virtualizes environment variables and current directory
    /// for use in multithreaded mode where tasks may be executed in parallel. This allows each project to maintain its own
    /// isolated environment state without affecting other concurrently building projects.
    /// </summary>
    /// <remarks>
    /// This class is not accessed from multiple threads. Each msbuild thread node has its own instance to work with.
    /// </remarks>
    internal sealed class MultiThreadedTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        private static readonly bool s_getTempPath2Available = IsGetTempPath2Available();

        private readonly Dictionary<string, string> _environmentVariables;
        private AbsolutePath _currentDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiThreadedTaskEnvironmentDriver"/> class
        /// with the specified working directory and optional environment variables.
        /// </summary>
        /// <param name="currentDirectoryFullPath">The initial working directory.</param>
        /// <param name="environmentVariables">Dictionary of environment variables to use.</param>
        public MultiThreadedTaskEnvironmentDriver(
            string currentDirectoryFullPath,
            IDictionary<string, string> environmentVariables)
        {
            _environmentVariables = new Dictionary<string, string>(environmentVariables, CommunicationsUtilities.EnvironmentVariableComparer);
            ProjectDirectory = new AbsolutePath(currentDirectoryFullPath, ignoreRootedCheck: true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiThreadedTaskEnvironmentDriver"/> class
        /// with the specified working directory and environment variables from the current process.
        /// </summary>
        /// <param name="currentDirectoryFullPath">The initial working directory.</param>
        public MultiThreadedTaskEnvironmentDriver(string currentDirectoryFullPath)
        {
            IDictionary variables = Environment.GetEnvironmentVariables();
            _environmentVariables = new Dictionary<string, string>(variables.Count, CommunicationsUtilities.EnvironmentVariableComparer);
            foreach (DictionaryEntry entry in variables)
            {
                _environmentVariables[(string)entry.Key] = (string)entry.Value!;
            }

            ProjectDirectory = new AbsolutePath(currentDirectoryFullPath, ignoreRootedCheck: true);
        }

        /// <inheritdoc/>
        public AbsolutePath ProjectDirectory
        {
            get => _currentDirectory;
            set
            {
                _currentDirectory = value.GetCanonicalForm();
                // Keep the thread-static in sync for use by Expander and Modifiers during property/item expansion.
                // This allows Path.GetFullPath and %(FullPath) functions used in project files to resolve relative paths correctly in multithreaded mode.
                FileUtilities.CurrentThreadWorkingDirectory = _currentDirectory.Value;
            }
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(path, ProjectDirectory);
        }

        /// <inheritdoc/>
        public AbsolutePath GetTempPath()
        {
            return GetTempPath(s_getTempPath2Available && IsSystemProcess());
        }

        internal AbsolutePath GetTempPath(bool useSystemTemp)
        {
            // Match Path.GetTempPath while reading task-local environment variables.
            // Unix source: https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/Path.Unix.cs
            if (!NativeMethods.IsWindows)
            {
                string? tempDirectory = GetEnvironmentVariable("TMPDIR");
                return FileUtilities.EnsureTrailingSlashWithoutNormalization(
                    GetAbsolutePath(string.IsNullOrEmpty(tempDirectory) ? "/tmp" : tempDirectory!));
            }

            // .NET uses GetTempPath2W when that export exists. It changes the result only for SYSTEM processes.
            // Windows source: https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/Path.Windows.cs
            if (useSystemTemp)
            {
                string systemTempDirectory = GetEnvironmentVariable("SystemTemp") ?? string.Empty;
                if (string.IsNullOrEmpty(systemTempDirectory))
                {
                    systemTempDirectory = Path.Combine(GetWindowsDirectory(), "SystemTemp");
                }

                return GetCanonicalTempPath(systemTempDirectory);
            }

            string tempDirectoryWindows = GetEnvironmentVariable("TMP") ?? string.Empty;
            if (string.IsNullOrEmpty(tempDirectoryWindows))
            {
                tempDirectoryWindows = GetEnvironmentVariable("TEMP") ?? string.Empty;
            }

            if (string.IsNullOrEmpty(tempDirectoryWindows))
            {
                tempDirectoryWindows = GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
            }

            if (string.IsNullOrEmpty(tempDirectoryWindows))
            {
                tempDirectoryWindows = GetWindowsDirectory();
            }

            return GetCanonicalTempPath(tempDirectoryWindows);
        }

        private AbsolutePath GetCanonicalTempPath(string tempDirectory)
        {
            return FileUtilities.EnsureTrailingSlashWithoutNormalization(
                GetAbsolutePath(tempDirectory).GetCanonicalForm());
        }

        private static string GetWindowsDirectory()
        {
            return Path.GetDirectoryName(Environment.SystemDirectory)!;
        }

        private static unsafe bool IsGetTempPath2Available()
        {
#if FEATURE_WINDOWSINTEROP && NET
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                return false;
            }

            const string Kernel32 = "kernel32.dll";
            fixed (char* moduleName = Kernel32)
            {
                HMODULE kernel32 = PInvoke.GetModuleHandle(moduleName);
                return !kernel32.IsNull && !PInvoke.GetProcAddress(kernel32, "GetTempPath2W").IsNull;
            }
#else
            return false;
#endif
        }

        private static bool IsSystemProcess()
        {
#if NET
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return identity.IsSystem;
#else
            return false;
#endif
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            return _environmentVariables.TryGetValue(name, out string? value) ? value : null;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _environmentVariables;
        }

        /// <inheritdoc/>
        public void SetEnvironmentVariable(string name, string? value)
        {
            if (value == null)
            {
                _environmentVariables.Remove(name);
            }
            else
            {
                _environmentVariables[name] = value;
            }
        }

        /// <inheritdoc/>
        public void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (ReferenceEquals(newEnvironment, _environmentVariables))
            {
                return;
            }

            // Simply replace the entire environment dictionary
            _environmentVariables.Clear();
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                _environmentVariables[entry.Key] = entry.Value;
            }
        }

        /// <inheritdoc/>
        public ProcessStartInfo GetProcessStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = ProjectDirectory.Value
            };

            // Set environment variables
            foreach (var kvp in _environmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            return startInfo;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Clear the thread-static to prevent pollution between builds on the same thread.
            FileUtilities.CurrentThreadWorkingDirectory = null;
        }
    }
}
