// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Implementation of <see cref="ITaskEnvironmentDriver"/> that virtualizes environment variables and current directory
    /// for use in multithreaded mode where tasks may be executed in parallel. This allows each project to maintain its own
    /// isolated environment state without affecting other concurrently building projects.
    /// </summary>
    internal sealed class MultiThreadedTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        /// <summary>
        /// String comparer for environment variable names based on the current platform.
        /// On Windows, environment variables are case-insensitive; on Unix-like systems, they are case-sensitive.
        /// </summary>
        private static readonly StringComparer EnvironmentVariableComparer = 
            NativeMethods.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
            _environmentVariables = new Dictionary<string, string>(environmentVariables, EnvironmentVariableComparer);
            _currentDirectory = new AbsolutePath(currentDirectoryFullPath, ignoreRootedCheck: true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiThreadedTaskEnvironmentDriver"/> class
        /// with the specified working directory and environment variables from the current process.
        /// </summary>
        /// <param name="currentDirectoryFullPath">The initial working directory.</param>
        public MultiThreadedTaskEnvironmentDriver(string currentDirectoryFullPath)
        {
            // Copy environment variables from the current process
            var variables = Environment.GetEnvironmentVariables();
            _environmentVariables = new Dictionary<string, string>(variables.Count, EnvironmentVariableComparer);

            foreach (string key in variables.Keys)
            {
                if (variables[key] is string value)
                {
                    _environmentVariables[key] = value;
                }
            }

            _currentDirectory = new AbsolutePath(currentDirectoryFullPath, ignoreRootedCheck: true);
        }

        public AbsolutePath ProjectDirectory
        {
            get => _currentDirectory;
            set => _currentDirectory = value;
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(path, ProjectDirectory);
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
                WorkingDirectory = ProjectDirectory.Path
            };

            // Set environment variables
            foreach (var kvp in _environmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            return startInfo;
        }
    }
}
