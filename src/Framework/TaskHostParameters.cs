// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

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
            IsEmptyParameters(parameters) ||
            (parameterName?.ToLowerInvariant() switch
            {
                nameof(Runtime) => string.IsNullOrEmpty(parameters.Runtime),
                nameof(Architecture) => string.IsNullOrEmpty(parameters.Architecture),
                nameof(DotnetHostPath) => string.IsNullOrEmpty(parameters.DotnetHostPath),
                nameof(MSBuildAssemblyPath) => string.IsNullOrEmpty(parameters.MSBuildAssemblyPath),
                _ => throw new ArgumentException($"Unknown parameter name: {parameterName}", nameof(parameterName))
            });

        /// <summary>
        /// Checks if all parameters in this instance are empty
        /// </summary>
        public static bool IsEmptyParameters(TaskHostParameters parameters) =>
            IsEmptyParameters(parameters) ||
            (string.IsNullOrEmpty(parameters.Runtime) &&
             string.IsNullOrEmpty(parameters.Architecture) &&
             string.IsNullOrEmpty(parameters.DotnetHostPath) &&
             string.IsNullOrEmpty(parameters.MSBuildAssemblyPath));

        /// <summary>
        /// Creates a TaskHostParameters from a dictionary for backward compatibility.
        /// </summary>
        public static TaskHostParameters FromDictionary(IDictionary<string, string> dictionary)
        {
            if (dictionary == null)
            {
                return new TaskHostParameters();
            }

            _ = dictionary.TryGetValue(nameof(Runtime), out string? runtime);
            _ = dictionary.TryGetValue(nameof(Architecture), out string? architecture);
            _ = dictionary.TryGetValue(nameof(DotnetHostPath), out string? dotnetHostPath);
            _ = dictionary.TryGetValue(nameof(MSBuildAssemblyPath), out string? msBuildAssemblyPath);

            return new TaskHostParameters(runtime, architecture, dotnetHostPath, msBuildAssemblyPath);
        }

        /// <summary>
        /// Converts the TaskHostParameters to a dictionary representation.
        /// </summary>
        /// When true, includes parameters with null or empty values in the dictionary.
        /// When false, only includes parameters that have non-empty values.
        /// <returns>
        /// A dictionary containing the parameter names as keys and their values as dictionary values.
        /// </returns>
        public static IDictionary<string, string> ToDictionary(TaskHostParameters parameters)
        {
            var dictionary = new Dictionary<string, string>(4, StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(parameters.Runtime))
            {
                dictionary[nameof(Runtime)] = parameters.Runtime;
            }

            if (!string.IsNullOrEmpty(parameters.Architecture))
            {
                dictionary[nameof(Architecture)] = parameters.Architecture;
            }

            if (!string.IsNullOrEmpty(parameters.DotnetHostPath))
            {
                dictionary[nameof(DotnetHostPath)] = parameters.DotnetHostPath;
            }

            if (!string.IsNullOrEmpty(parameters.MSBuildAssemblyPath))
            {
                dictionary[nameof(MSBuildAssemblyPath)] = parameters.MSBuildAssemblyPath;
            }

            return dictionary;
        }
    }
}
