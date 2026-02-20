// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Classifies environment variables to prevent modification of those that MSBuild assumes remain constant.
    /// These variables should not be modified during the build process,
    /// particularly in multi-threaded build scenarios.
    /// </summary>
    internal static class EnvironmentVariableClassifier
    {

        /// <summary>
        /// Set of specific environment variable names that MSBuild assumes should not be modified.
        /// </summary>
        private static readonly Lazy<FrozenSet<string>> s_immutableVariables = new Lazy<FrozenSet<string>>(() =>
            FrozenSet.ToFrozenSet([
                // .NET Framework path resolution - used by FrameworkLocationHelper
                EnvironmentVariablesNames.ComplusInstallRoot,
                EnvironmentVariablesNames.ComplusVersion,
                
                // Reference assembly root path - used by FrameworkLocationHelper
                EnvironmentVariablesNames.ReferenceAssemblyRoot,
                
                // Program Files directories - used by ToolLocationHelper for SDK/tool discovery
                EnvironmentVariablesNames.ProgramW6432,
                EnvironmentVariablesNames.ProgramFiles,
                
                // .NET host path - used by ToolLocationHelper for .NET Core/.NET 5+ discovery
                EnvironmentVariablesNames.DotnetHostPath
            ], FrameworkFileUtilities.EnvironmentVariableComparer));

        /// <summary>
        /// Gets whether the specified environment variable is one that MSBuild assumes should not be modified.
        /// </summary>
        /// <param name="name">The environment variable name to check.</param>
        /// <returns>True if the variable is immutable, false otherwise.</returns>
        internal static bool IsImmutable(string name)
        {
            // Check specific variables that MSBuild assumes are constant
            if (s_immutableVariables.Value.Contains(name))
            {
                return true;
            }

            // All variables that start with "MSBUILD" are assumed to be immutable
            return name.StartsWith("MSBUILD", FrameworkFileUtilities.EnvironmentVariableComparison);
        }
    }
}
