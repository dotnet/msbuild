// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Classifies environment variables as immutable or mutable.
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
        /// Deferred creation avoids overhead in multi-process builds where environment virtualization is not used.
        /// </remarks>
        private static readonly Lazy<EnvironmentVariableClassifier> s_instance = new(() => new EnvironmentVariableClassifier());

        /// <summary>
        /// Gets the shared singleton instance used for classifying environment variables during builds.
        /// </summary>
        internal static EnvironmentVariableClassifier Instance => s_instance.Value;

        /// <summary>
        /// Set of specific environment variable names that are classified as immutable.
        /// </summary>
        private readonly FrozenSet<string> _immutableVariables;

        /// <summary>
        /// Prefixes that identify immutable environment variables.
        /// Any variable starting with one of these prefixes is considered immutable.
        /// </summary>
        private readonly IReadOnlyList<string> _immutablePrefixes;

        /// <summary>
        /// Initializes a new instance with the default set of immutable environment variables and prefixes.
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
            
            // On case-sensitive systems, both "MSBUILD" and "MSBuild" prefixes are used
            var prefixSet = new HashSet<string>(FrameworkFileUtilities.EnvironmentVariableComparer) { "MSBUILD", "MSBuild" };
            _immutablePrefixes = new List<string>(prefixSet);
        }

        /// <summary>
        /// Initializes a new instance with a custom set of immutable environment variables and prefixes.
        /// </summary>
        /// <param name="immutableVariables">Set of environment variable names to treat as immutable.</param>
        /// <param name="immutablePrefixes">Prefixes that identify immutable environment variables. If null or empty, no prefix matching is performed.</param>
        /// <remarks>
        /// This constructor is primarily intended for testing scenarios where custom immutability rules are needed.
        /// </remarks>
        internal EnvironmentVariableClassifier(IEnumerable<string> immutableVariables, string[] immutablePrefixes)
        {
            _immutableVariables = FrozenSet.ToFrozenSet(immutableVariables, FrameworkFileUtilities.EnvironmentVariableComparer);
            _immutablePrefixes = immutablePrefixes ?? [];
        }

        /// <summary>
        /// Determines whether the specified environment variable is classified as immutable.
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
