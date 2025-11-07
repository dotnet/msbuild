// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A readonly struct that represents task host parameters used to determine which host process to launch.
    /// </summary>
    public readonly struct TaskHostParameters
    {
        /// <summary>
        /// A static empty instance to avoid allocations when default parameters are needed.
        /// </summary>
        public static readonly TaskHostParameters Empty = new();

        private readonly string? _runtime;
        private readonly string? _architecture;
        private readonly string? _dotnetHostPath;
        private readonly string? _msBuildAssemblyPath;
        private readonly bool? _taskHostFactoryExplicitlyRequested;

        /// <summary>
        /// Initializes a new instance of the TaskHostParameters struct with the specified parameters.
        /// </summary>
        /// <param name="runtime">The target runtime identifier (e.g., "net8.0", "net472").</param>
        /// <param name="architecture">The target architecture (e.g., "x64", "x86", "arm64").</param>
        /// <param name="dotnetHostPath">The path to the dotnet host executable.</param>
        /// <param name="msBuildAssemblyPath">The path to the MSBuild assembly.</param>
        /// <param name="taskHostFactoryExplicitlyRequested">Defines if Task Host Factory was explicitly requested.</param>
        internal TaskHostParameters(
            string? runtime = null,
            string? architecture = null,
            string? dotnetHostPath = null,
            string? msBuildAssemblyPath = null,
            bool? taskHostFactoryExplicitlyRequested = null)
        {
            _runtime = runtime;
            _architecture = architecture;
            _dotnetHostPath = dotnetHostPath;
            _msBuildAssemblyPath = msBuildAssemblyPath;
            _taskHostFactoryExplicitlyRequested = taskHostFactoryExplicitlyRequested;
        }

        /// <summary>
        /// Gets the target runtime identifier (e.g., "net8.0", "net472").
        /// </summary>
        /// <value>The runtime identifier, or null if not specified.</value>
        public string? Runtime => _runtime;

        /// <summary>
        /// Gets the target architecture (e.g., "x64", "x86", "arm64").
        /// </summary>
        /// <value>The architecture identifier, or an empty string if not specified.</value>
        public string? Architecture => _architecture;

        /// <summary>
        /// Gets the path to the dotnet host executable.
        /// </summary>
        /// <value>The dotnet host path, or null if not specified.</value>
        public string? DotnetHostPath => _dotnetHostPath;

        /// <summary>
        /// Gets the path to the MSBuild assembly.
        /// </summary>
        /// <value>The MSBuild assembly path, or null if not specified.</value>
        public string? MSBuildAssemblyPath => _msBuildAssemblyPath;

        /// <summary>
        /// Gets if Task Host Factory was requested explicitly (by specifying TaskHost="TaskHostFactory" in UsingTask element).
        /// </summary>
        public bool? TaskHostFactoryExplicitlyRequested => _taskHostFactoryExplicitlyRequested;

        /// <summary>
        /// Gets a value indicating whether returns true if parameters were unset.
        /// </summary>
        internal bool IsEmpty => Equals(Empty);

        /// <summary>
        /// Merges two TaskHostParameters instances, with the second parameter values taking precedence when both are specified.
        /// </summary>
        /// <param name="baseParameters">The base parameters.</param>
        /// <param name="overrideParameters">The override parameters that take precedence.</param>
        /// <returns>A new TaskHostParameters with merged values.</returns>
        internal static TaskHostParameters MergeTaskHostParameters(TaskHostParameters baseParameters, TaskHostParameters overrideParameters)
        {
            // If both are empty, return empty
            if (baseParameters.IsEmpty && overrideParameters.IsEmpty)
            {
                return Empty;
            }

            // If override is empty, return base
            if (overrideParameters.IsEmpty)
            {
                return baseParameters;
            }

            // If base is empty, return override
            if (baseParameters.IsEmpty)
            {
                return overrideParameters;
            }

            // Merge: override values take precedence, fall back to base values
            return new TaskHostParameters(
                runtime: overrideParameters.Runtime ?? baseParameters.Runtime,
                architecture: overrideParameters.Architecture ?? baseParameters.Architecture,
                dotnetHostPath: overrideParameters.DotnetHostPath ?? baseParameters.DotnetHostPath,
                msBuildAssemblyPath: overrideParameters.MSBuildAssemblyPath ?? baseParameters.MSBuildAssemblyPath,
                taskHostFactoryExplicitlyRequested: overrideParameters.TaskHostFactoryExplicitlyRequested ?? baseParameters.TaskHostFactoryExplicitlyRequested);
        }

        /// <summary>
        /// Creates a new instance of <see cref="TaskHostParameters"/> with the specified value for the
        /// <see cref="TaskHostFactoryExplicitlyRequested"/> property.
        /// </summary>
        internal TaskHostParameters WithTaskHostFactoryExplicitlyRequested(bool taskHostFactoryExplicitlyRequested)
        {
            if (_taskHostFactoryExplicitlyRequested == taskHostFactoryExplicitlyRequested)
            {
                return this;
            }

            return new TaskHostParameters(
                runtime: _runtime,
                architecture: _architecture,
                dotnetHostPath: _dotnetHostPath,
                msBuildAssemblyPath: _msBuildAssemblyPath,
                taskHostFactoryExplicitlyRequested: taskHostFactoryExplicitlyRequested);
        }

        /// <summary>
        /// The method was added to sustain compatibility with ITaskFactory2 factoryIdentityParameters parameters dictionary.
        /// </summary>
        internal Dictionary<string, string> ToDictionary() => new(3)
        {
            { nameof(Runtime), Runtime ?? string.Empty },
            { nameof(Architecture), Architecture ?? string.Empty },
            { nameof(TaskHostFactoryExplicitlyRequested), TaskHostFactoryExplicitlyRequested?.ToString() ?? string.Empty },
        };
    }
}
