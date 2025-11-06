// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Internal interface for managing task execution environment, including environment variables and working directory.
    /// </summary>
    /// <remarks>
    /// If we ever consider making any part of this API public, strongly consider making an abstract class instead of a public interface.
    /// </remarks>
    internal interface ITaskEnvironmentDriver
    {
        /// <summary>
        /// Gets or sets the current working directory for the task environment.
        /// </summary>
        AbsolutePath ProjectDirectory { get; internal set; }

        /// <summary>
        /// Gets an absolute path from the specified path, resolving relative paths against the current project directory.
        /// </summary>
        /// <param name="path">The path to convert to absolute.</param>
        /// <returns>An absolute path representation.</returns>
        AbsolutePath GetAbsolutePath(string path);

        /// <summary>
        /// Gets the value of the specified environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, or null if not found.</returns>
        string? GetEnvironmentVariable(string name);

        /// <summary>
        /// Gets all environment variables for this task environment.
        /// </summary>
        /// <returns>A read-only dictionary of environment variable names and values.</returns>
        IReadOnlyDictionary<string, string> GetEnvironmentVariables();

        /// <summary>
        /// Sets an environment variable to the specified value.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value to set, or null to remove the variable.</param>
        void SetEnvironmentVariable(string name, string? value);

        /// <summary>
        /// Sets the environment to match the specified collection of variables.
        /// Removes variables not present in the new environment and updates or adds those that are.
        /// </summary>
        /// <param name="newEnvironment">The new environment variable collection.</param>
        void SetEnvironment(IDictionary<string, string> newEnvironment);

        /// <summary>
        /// Gets a ProcessStartInfo configured with the current environment and working directory.
        /// </summary>
        /// <returns>A ProcessStartInfo with the current environment settings.</returns>
        ProcessStartInfo GetProcessStartInfo();
    }
}