// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Default implementation of <see cref="ITaskEnvironmentDriver"/> that directly interacts with the file system
    /// and environment variables. Used in multi-process mode of execution.
    /// </summary>
    /// <remarks>
    /// Implemented as a singleton since it has no instance state.
    /// </remarks>
    internal sealed class StubTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        private static readonly StubTaskEnvironmentDriver s_instance = new StubTaskEnvironmentDriver();

        /// <summary>
        /// Gets the singleton instance of StubTaskEnvironmentDriver.
        /// </summary>
        public static StubTaskEnvironmentDriver Instance => s_instance;

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private StubTaskEnvironmentDriver() { }

        /// <inheritdoc/>
        public AbsolutePath ProjectDirectory 
        { 
            get => new AbsolutePath(Directory.GetCurrentDirectory(), ignoreRootedCheck: true);
            set => Directory.SetCurrentDirectory(value.Path);
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(Path.GetFullPath(path), ignoreRootedCheck: true);
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            var variables = Environment.GetEnvironmentVariables();
            // On Windows, environment variables are case-insensitive; on Unix-like systems, they are case-sensitive
            var comparer = NativeMethods.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var result = new Dictionary<string, string>(variables.Count, comparer);

            foreach (string key in variables.Keys)
            {
                if (variables[key] is string value)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public void SetEnvironmentVariable(string name, string? value)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        /// <inheritdoc/>
        public void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            // First, delete all no longer set variables
            IReadOnlyDictionary<string, string> currentEnvironment = GetEnvironmentVariables();
            foreach (KeyValuePair<string, string> entry in currentEnvironment)
            {
                if (!newEnvironment.ContainsKey(entry.Key))
                {
                    SetEnvironmentVariable(entry.Key, null);
                }
            }

            // Then, make sure the new ones have their new values.
            foreach (KeyValuePair<string, string> entry in newEnvironment)
            {
                if (!currentEnvironment.TryGetValue(entry.Key, out string? currentValue) || currentValue != entry.Value)
                {
                    SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }
        }

        /// <inheritdoc/>
        public ProcessStartInfo GetProcessStartInfo()
        {
            return new ProcessStartInfo();
        }
    }
}
