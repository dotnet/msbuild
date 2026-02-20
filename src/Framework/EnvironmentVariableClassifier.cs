// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Classifies environment variables to prevent modification of those that MSBuild assumes remain constant.
    /// These variables should not be modified during the build process.
    /// </summary>
    /// <remarks>
    /// Used in multithreaded build scenarios to prevent tasks from modifying environment variables that could affect other concurrently building projects. 
    /// </remarks>
    internal sealed class EnvironmentVariableClassifier
    {
        /// <summary>
        /// Shared instance used by MSBuild for environment variable classification.
        /// </summary>
        /// <remarks>
        /// Deferred creation avoids overhead in multiprocess builds where this is not to be used.
        /// </remarks>
        private static readonly Lazy<EnvironmentVariableClassifier> s_instance = new(() => new EnvironmentVariableClassifier());

        /// <summary>
        /// Shared instance used by MSBuild for production environment variable classification.
        /// </summary>
        internal static EnvironmentVariableClassifier Instance => s_instance.Value;

        /// <summary>
        /// Set of specific environment variable names that MSBuild assumes should not be modified.
        /// </summary>
        private readonly FrozenSet<string> _immutableVariables;

        /// <summary>
        /// Array of prefixes that identify immutable environment variables.
        /// </summary>
        private readonly string[] _immutablePrefixes;

        /// <summary>
        /// Initializse a new instance with the default set of immutable environment variables and prefixes.
        /// </summary>
        private EnvironmentVariableClassifier()
        {
            _immutableVariables = FrozenSet.ToFrozenSet([
                // Environment variables used by FrameworkLocationHelper and ToolLocationHelper for framework/SDK discovery.
                EnvironmentVariablesNames.ComplusInstallRoot,
                EnvironmentVariablesNames.ComplusVersion,
                EnvironmentVariablesNames.ReferenceAssemblyRoot,
                EnvironmentVariablesNames.ProgramW6432
            ], FrameworkFileUtilities.EnvironmentVariableComparer);
            
            _immutablePrefixes = ["MSBUILD"];
        }

        /// <summary>
        /// Initializes a new instance with a custom set of immutable environment variables and prefixes.
        /// Used primarily for testing scenarios.
        /// </summary>
        /// <param name="immutableVariables">Custom set of environment variable names to treat as immutable.</param>
        /// <param name="immutablePrefixes">Array of prefixes that identify immutable environment variables. If null or empty, no prefix matching is performed.</param>
        internal EnvironmentVariableClassifier(IEnumerable<string> immutableVariables, string[] immutablePrefixes)
        {
            _immutableVariables = FrozenSet.ToFrozenSet(immutableVariables, FrameworkFileUtilities.EnvironmentVariableComparer);
            _immutablePrefixes = immutablePrefixes ?? [];
        }

        /// <summary>
        /// Gets whether the specified environment variable is one that MSBuild assumes should not be modified.
        /// </summary>
        /// <param name="name">The environment variable name to check.</param>
        /// <returns>True if the variable is immutable, false otherwise.</returns>
        internal bool IsImmutable(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // Check specific variables that are configured as constant
            if (_immutableVariables.Contains(name))
            {
                return true;
            }

            // Check if variable starts with any of the configured immutable prefixes
            foreach (string prefix in _immutablePrefixes)
            {
                if (name.StartsWith(prefix, FrameworkFileUtilities.EnvironmentVariableComparison))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
