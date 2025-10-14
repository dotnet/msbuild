// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents an absolute path that ensures path operations are performed correctly
    /// in multithreaded environments by avoiding reliance on current working directory.
    /// </summary>
    public readonly struct AbsolutePath
    {
        /// <summary>
        /// Gets the absolute path string. Returns empty string for default value.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Internal constructor that can bypass rooted path checking.
        /// </summary>
        /// <param name="path">The path string</param>
        /// <param name="ignoreRootedCheck">If true, bypasses the rooted path validation</param>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
        {
            Path = path ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of AbsolutePath with validation that the path is rooted.
        /// </summary>
        /// <param name="path">The absolute path string</param>
        /// <exception cref="ArgumentException">Thrown when the path is not rooted</exception>
        public AbsolutePath(string path)
        {
            if (!string.IsNullOrEmpty(path) && !System.IO.Path.IsPathRooted(path))
            {
                throw new ArgumentException("Path must be absolute (rooted)", nameof(path));
            }
            Path = path ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance by combining a relative path with an absolute base path.
        /// </summary>
        /// <param name="path">The relative or absolute path</param>
        /// <param name="basePath">The absolute base path</param>
        public AbsolutePath(string path, AbsolutePath basePath)
        {
            if (string.IsNullOrEmpty(path))
            {
                Path = string.Empty;
            }
            else if (System.IO.Path.IsPathRooted(path))
            {
                Path = path;
            }
            else
            {
                Path = System.IO.Path.Combine(basePath.Path, path);
            }
        }

        /// <summary>
        /// Implicitly converts AbsolutePath to string for seamless integration with existing APIs.
        /// </summary>
        /// <param name="path">The AbsolutePath to convert</param>
        public static implicit operator string(AbsolutePath path)
        {
            return path.Path;
        }

        /// <summary>
        /// Returns the path string.
        /// </summary>
        /// <returns>The absolute path string</returns>
        public override string ToString() => Path;
    }

    /// <summary>
    /// Provides thread-safe alternatives to APIs that use global process state,
    /// enabling tasks to execute safely in a multithreaded environment.
    /// </summary>
    public class TaskEnvironment
    {
        /// <summary>
        /// Gets or sets the project directory for resolving relative paths.
        /// </summary>
        public virtual AbsolutePath ProjectCurrentDirectory { get; set; }

        /// <summary>
        /// Resolves paths relative to ProjectCurrentDirectory, ensuring thread-safe path resolution.
        /// </summary>
        /// <param name="path">The path to resolve (can be relative or absolute)</param>
        /// <returns>An absolute path</returns>
        public virtual AbsolutePath GetAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new AbsolutePath(string.Empty, ignoreRootedCheck: true);
            }

            if (System.IO.Path.IsPathRooted(path))
            {
                return new AbsolutePath(path, ignoreRootedCheck: true);
            }

            return new AbsolutePath(path, ProjectCurrentDirectory);
        }

        /// <summary>
        /// Gets an environment variable value in a thread-safe manner.
        /// </summary>
        /// <param name="name">The name of the environment variable</param>
        /// <returns>The value of the environment variable, or null if not found</returns>
        public virtual string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        /// <summary>
        /// Gets all environment variables in a thread-safe manner.
        /// </summary>
        /// <returns>A read-only dictionary of environment variables</returns>
        public virtual IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            var envVars = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    envVars[key] = value;
                }
            }
            return envVars;
        }

        /// <summary>
        /// Sets an environment variable for this task environment.
        /// This does not modify the global process environment.
        /// </summary>
        /// <param name="name">The name of the environment variable</param>
        /// <param name="value">The value to set, or null to remove the variable</param>
        public virtual void SetEnvironmentVariable(string name, string value)
        {
            // In a full implementation, this would maintain a task-specific environment
            // For now, this is a placeholder that doesn't modify global state
            // The actual implementation would store these in a task-local dictionary
            throw new NotImplementedException("Task-specific environment variable setting is not yet implemented");
        }

        /// <summary>
        /// Gets a ProcessStartInfo configured with the appropriate environment and working directory.
        /// </summary>
        /// <returns>A ProcessStartInfo object configured for thread-safe execution</returns>
        public virtual ProcessStartInfo GetProcessStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = ProjectCurrentDirectory.Path,
                UseShellExecute = false
            };

            // Copy current environment variables
            foreach (var envVar in GetEnvironmentVariables())
            {
                startInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
            }

            return startInfo;
        }
    }
}