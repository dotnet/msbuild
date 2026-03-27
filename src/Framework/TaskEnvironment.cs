// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides an <see cref="IMultiThreadableTask"/> with access to a run-time execution environment including
    /// environment variables, file paths, and process management capabilities.
    /// </summary>
    public sealed class TaskEnvironment
    {
        private readonly ITaskEnvironmentDriver _driver;

        /// <summary>
        /// Initializes a new instance of the TaskEnvironment class.
        /// </summary>
        internal TaskEnvironment(ITaskEnvironmentDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Gets the fallback task environment that directly accesses the system environment variables
        /// and working directory of the current process.
        /// </summary>
        /// <remarks>
        /// This is the environment provided to tasks by the MSBuild engine in multi-process execution mode,
        /// where each task runs in its own process and process-level state is inherently isolated.
        /// </remarks>
        public static TaskEnvironment Fallback { get; } = new(MultiProcessTaskEnvironmentDriver.Instance);

        /// <summary>
        /// Creates a new <see cref="TaskEnvironment"/> with isolated working directory and environment variables.
        /// </summary>
        /// <remarks>
        /// This method is primarily intended for testing scenarios. In normal MSBuild operation, the correct task environment is provided by the MSBuild engine.
        /// The created TaskEnvironment provides isolated environment state similar to what tasks receive in multithreaded execution mode, enabling testing of task isolation behavior.
        /// </remarks>
        /// <param name="projectDirectory">The initial working directory for the task.</param>
        /// <param name="environmentVariables">A dictionary of environment variables to use, or <see langword="null"/> to use the current process environment variables.</param>
        /// <returns>A new <see cref="TaskEnvironment"/> with isolated environment state.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="projectDirectory"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="projectDirectory"/> is empty.</exception>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static TaskEnvironment CreateWithProjectDirectoryAndEnvironment(string projectDirectory, IDictionary<string, string>? environmentVariables = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(projectDirectory);

            return environmentVariables is null
                ? new TaskEnvironment(new MultiThreadedTaskEnvironmentDriver(projectDirectory))
                : new TaskEnvironment(new MultiThreadedTaskEnvironmentDriver(projectDirectory, environmentVariables));
        }

        /// <summary>
        /// Gets or sets the project directory for the task execution.
        /// </summary>
        public AbsolutePath ProjectDirectory
        {
            get => _driver.ProjectDirectory;
            internal set => _driver.ProjectDirectory = value;
        }

        /// <summary>
        /// Converts a relative or absolute path string to an absolute path.
        /// This function resolves paths relative to <see cref="ProjectDirectory"/>.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>An absolute path representation.</returns>
        public AbsolutePath GetAbsolutePath(string path) => _driver.GetAbsolutePath(path);

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, or null if it does not exist.</returns>
        public string? GetEnvironmentVariable(string name) => _driver.GetEnvironmentVariable(name);

        /// <summary>
        /// Gets a dictionary containing all environment variables.
        /// </summary>
        /// <returns>A read-only dictionary of environment variables.</returns>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables() => _driver.GetEnvironmentVariables();

        /// <summary>
        /// Sets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value to set, or null to remove the environment variable.</param>
        public void SetEnvironmentVariable(string name, string? value) => _driver.SetEnvironmentVariable(name, value);

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// This mirrors the behavior of CommunicationsUtilities.SetEnvironment but operates on this TaskEnvironment.
        /// </summary>
        /// <param name="newEnvironment">The new environment variables to set.</param>
        internal void SetEnvironment(IDictionary<string, string> newEnvironment) => _driver.SetEnvironment(newEnvironment);

        /// <summary>
        /// Creates a new ProcessStartInfo configured for the current task execution environment.
        /// </summary>
        /// <returns>A ProcessStartInfo object configured for the current task execution environment.</returns>
        public ProcessStartInfo GetProcessStartInfo() => _driver.GetProcessStartInfo();

        /// <summary>
        /// Disposes the underlying driver, cleaning up any thread-local state.
        /// </summary>
        internal void Dispose() => _driver.Dispose();
    }
}
