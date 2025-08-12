// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A readonly struct that represents task host parameters used to determine which host process to launch.
    /// This struct provides a performance-optimized alternative to Dictionary&lt;string, string&gt; by avoiding
    /// allocations for small, known parameter sets while maintaining API compatibility.
    /// </summary>
    public readonly struct TaskHostParameters
    {
        private readonly string? _runtime;
        private readonly string? _architecture;
        private readonly string? _dotnetHostPath;
        private readonly string? _msBuildAssemblyPath;

        /// <summary>
        /// A static empty instance to avoid allocations when default parameters are needed.
        /// </summary>
        public static readonly TaskHostParameters Empty = new();

        /// <summary>
        /// Initializes a new instance of the TaskHostParameters struct with the specified parameters.
        /// </summary>
        /// <param name="runtime">The target runtime identifier (e.g., "net8.0", "net472").</param>
        /// <param name="architecture">The target architecture (e.g., "x64", "x86", "arm64").</param>
        /// <param name="dotnetHostPath">The path to the dotnet host executable.</param>
        /// <param name="msBuildAssemblyPath">The path to the MSBuild assembly.</param>
        public TaskHostParameters(string? runtime = null, string? architecture = null, string? dotnetHostPath = null, string? msBuildAssemblyPath = null)
        {
            _runtime = runtime;
            _architecture = architecture;
            _dotnetHostPath = dotnetHostPath;
            _msBuildAssemblyPath = msBuildAssemblyPath;
        }

        /// <summary>
        /// Gets the target runtime identifier (e.g., "net8.0", "net472").
        /// </summary>
        /// <value>The runtime identifier, or an empty string if not specified.</value>
        public string? Runtime => _runtime;

        /// <summary>
        /// Gets the target architecture (e.g., "x64", "x86", "arm64").
        /// </summary>
        /// <value>The architecture identifier, or an empty string if not specified.</value>
        public string? Architecture => _architecture;

        /// <summary>
        /// Gets the path to the dotnet host executable.
        /// </summary>
        /// <value>The dotnet host path, or an empty string if not specified.</value>
        public string? DotnetHostPath => _dotnetHostPath;

        /// <summary>
        /// Gets the path to the MSBuild assembly.
        /// </summary>
        /// <value>The MSBuild assembly path, or an empty string if not specified.</value>
        public string? MSBuildAssemblyPath => _msBuildAssemblyPath;

        /// <summary>
        /// Determines whether a specific parameter in the TaskHostParameters instance is empty or null.
        /// </summary>
        /// <param name="parameters">The TaskHostParameters instance to check.</param>
        /// <param name="parameterName">The name of the parameter to check (Runtime, Architecture, DotnetHostPath, or MSBuildAssemblyPath).</param>
        /// <returns><c>true</c> if all parameters are empty or the specified parameter is empty; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when the parameter name is not recognized.</exception>
        public static bool IsEmptyParameter(TaskHostParameters parameters, string parameterName) =>
            parameterName switch
            {
                nameof(Runtime) => string.IsNullOrEmpty(parameters.Runtime),
                nameof(Architecture) => string.IsNullOrEmpty(parameters.Architecture),
                nameof(DotnetHostPath) => string.IsNullOrEmpty(parameters.DotnetHostPath),
                nameof(MSBuildAssemblyPath) => string.IsNullOrEmpty(parameters.MSBuildAssemblyPath),
                _ => throw new ArgumentException($"Unknown parameter name: {parameterName}", nameof(parameterName))
            };

        /// <summary>
        /// Determines whether all parameters in the TaskHostParameters instance are empty or null.
        /// </summary>
        /// <param name="parameters">The TaskHostParameters instance to check.</param>
        /// <returns><c>true</c> if all parameters (Runtime, Architecture, DotnetHostPath, and MSBuildAssemblyPath) are empty or null; otherwise, <c>false</c>.</returns>
        public static bool IsEmptyParameters(TaskHostParameters parameters) =>
            string.IsNullOrEmpty(parameters.Runtime) &&
            string.IsNullOrEmpty(parameters.Architecture) &&
            string.IsNullOrEmpty(parameters.DotnetHostPath) &&
            string.IsNullOrEmpty(parameters.MSBuildAssemblyPath);
    }
}
