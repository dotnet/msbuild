// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
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
        /// Validates that the specified environment variable can be modified.
        /// Throws if the variable is one that MSBuild assumes should remain constant.
        /// </summary>
        /// <param name="name">The name of the environment variable to check.</param>
        /// <exception cref="ArgumentException">Thrown when attempting to modify an immutable environment variable.</exception>
        private void EnsureVariableCanBeModified(string name)
        {
            if (EnvironmentVariableClassifier.IsImmutable(name))
            {
                throw new ArgumentException(
                    $"Task cannot modify environment variable '{name}' because MSBuild assumes it should remain constant.",
                    nameof(name));
            }
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
                FrameworkFileUtilities.CurrentThreadWorkingDirectory = _currentDirectory.Value;
            }
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
            // Only validate if we're actually changing the value
            _environmentVariables.TryGetValue(name, out string? currentValue);
            if (!CommunicationsUtilities.EnvironmentVariableComparer.Equals(currentValue, value))
            {
                EnsureVariableCanBeModified(name);
            }
            
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
            // Check for variables being removed (exist in current but not in new environment)
            foreach (string currentVar in _environmentVariables.Keys)
            {
                if (!newEnvironment.ContainsKey(currentVar))
                {
                    EnsureVariableCanBeModified(currentVar);
                }
            }
            
            // Check for variables being added or modified
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                _environmentVariables.TryGetValue(entry.Key, out string? currentValue);
                
                // Only validate if we're actually changing the value
                if (!CommunicationsUtilities.EnvironmentVariableComparer.Equals(currentValue, entry.Value))
                {
                    EnsureVariableCanBeModified(entry.Key);
                }
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
            FrameworkFileUtilities.CurrentThreadWorkingDirectory = null;
        }
    }
}
