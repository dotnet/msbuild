// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Implementation of <see cref="ITaskEnvironmentDriver"/> that virtualizes environment variables and current directory
    /// for use in thread nodes where tasks may be executed in parallel. This allows each project to maintain its own
    /// isolated environment state without affecting other concurrently building projects.
    /// </summary>
    internal sealed class MultithreadedTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        private readonly Dictionary<string, string> _environmentVariables;
        private AbsolutePath _currentDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultithreadedTaskEnvironmentDriver"/> class
        /// with the specified working directory and optional environment variables.
        /// </summary>
        /// <param name="currentDirectoryFullPath">The initial working directory.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to use. 
        /// If not provided, the current environment variables are used.</param>
        public MultithreadedTaskEnvironmentDriver(
            string currentDirectoryFullPath,
            Dictionary<string, string> environmentVariables)
        {
            _environmentVariables = environmentVariables;
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
