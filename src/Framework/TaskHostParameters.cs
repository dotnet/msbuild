// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    public readonly struct TaskHostParameters
    {
        private readonly string _runtime;
        private readonly string _architecture;
        private readonly string _dotnetHostPath;
        private readonly string _msBuildAssemblyPath;

        public TaskHostParameters(string? runtime = null, string? architecture = null, string? dotnetHostPath = null, string? msBuildAssemblyPath = null)
        {
            _runtime = runtime ?? string.Empty;
            _architecture = architecture ?? string.Empty;
            _dotnetHostPath = dotnetHostPath ?? string.Empty;
            _msBuildAssemblyPath = msBuildAssemblyPath ?? string.Empty;
        }

        public string Runtime => _runtime ?? string.Empty;

        public string Architecture => _architecture ?? string.Empty;

        public string DotnetHostPath => _dotnetHostPath ?? string.Empty;

        public string MSBuildAssemblyPath => _msBuildAssemblyPath ?? string.Empty;

        /// <summary>
        /// Checks if a specific parameter by name is empty or null
        /// </summary>
        public static bool IsEmptyParameter(TaskHostParameters parameters, string parameterName) =>
            parameterName?.ToLowerInvariant() switch
            {
                nameof(Runtime) => string.IsNullOrEmpty(parameters.Runtime),
                nameof(Architecture) => string.IsNullOrEmpty(parameters.Architecture),
                nameof(DotnetHostPath) => string.IsNullOrEmpty(parameters.DotnetHostPath),
                nameof(MSBuildAssemblyPath) => string.IsNullOrEmpty(parameters.MSBuildAssemblyPath),
                _ => throw new ArgumentException($"Unknown parameter name: {parameterName}", nameof(parameterName))
            };

        /// <summary>
        /// Checks if all parameters in this instance are empty
        /// </summary>
        public static bool IsEmptyParameters(TaskHostParameters parameters) =>
            string.IsNullOrEmpty(parameters.Runtime) &&
            string.IsNullOrEmpty(parameters.Architecture) &&
            string.IsNullOrEmpty(parameters.DotnetHostPath) &&
            string.IsNullOrEmpty(parameters.MSBuildAssemblyPath);
    }
}
