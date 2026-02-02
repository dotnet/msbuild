// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Helper methods for managing DOTNET_ROOT environment variables during MSBuild app host bootstrap.
    /// When MSBuild runs as an app host (native executable), child processes need DOTNET_ROOT set
    /// to find the runtime, but this should not leak to tools those processes execute.
    /// </summary>
    internal static class DotnetHostEnvironmentHelper
    {
        // Environment variable names for .NET runtime root directories.
        // Duplicated here because this file is shared across assemblies and Constants class is not always accessible.
        private const string DotnetRootEnvVarName = "DOTNET_ROOT";
        private const string DotnetRootX64EnvVarName = "DOTNET_ROOT_X64";
        private const string DotnetRootX86EnvVarName = "DOTNET_ROOT_X86";
        private const string DotnetRootArm64EnvVarName = "DOTNET_ROOT_ARM64";

        /// <summary>
        /// Clears DOTNET_ROOT environment variables that were set only for app host bootstrap.
        /// These should not leak to tools executed by worker nodes.
        /// Only clears if the variable was NOT present in the original build process environment.
        /// </summary>
        /// <param name="buildProcessEnvironment">The original environment from the entry-point process.</param>
        internal static void ClearBootstrapDotnetRootEnvironment(IDictionary<string, string> buildProcessEnvironment)
        {
            string[] dotnetRootVars = [DotnetRootEnvVarName, DotnetRootX64EnvVarName, DotnetRootX86EnvVarName, DotnetRootArm64EnvVarName];
            foreach (string varName in dotnetRootVars)
            {
                if (!buildProcessEnvironment.ContainsKey(varName))
                {
                    Environment.SetEnvironmentVariable(varName, null);
                }
            }
        }

        /// <summary>
        /// Creates environment variable overrides for app host bootstrap.
        /// Sets DOTNET_ROOT derived from DOTNET_HOST_PATH and clears architecture-specific variants.
        /// </summary>
        /// <param name="dotnetHostPath">Optional path to the dotnet executable. If null, reads from DOTNET_HOST_PATH environment variable.</param>
        /// <param name="throwIfNotSet">If true, throws when dotnetHostPath is not available. If false, returns null.</param>
        /// <returns>Dictionary of environment variable overrides, or null if DOTNET_HOST_PATH is not set and throwIfNotSet is false.</returns>
        internal static IDictionary<string, string>? CreateDotnetRootEnvironmentOverrides(string? dotnetHostPath = null, bool throwIfNotSet = false)
        {
            dotnetHostPath ??= Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

            if (string.IsNullOrEmpty(dotnetHostPath))
            {
                if (throwIfNotSet)
                {
                    throw new InvalidOperationException(ResourceUtilities.GetResourceString("DotnetHostPathNotSet"));
                }

                // DOTNET_HOST_PATH not set - not running under SDK, no overrides needed
                return null;
            }

            return new Dictionary<string, string>
            {
                [DotnetRootEnvVarName] = Path.GetDirectoryName(dotnetHostPath)!,
                // Clear architecture-specific overrides that would take precedence over DOTNET_ROOT
                [DotnetRootX64EnvVarName] = null!,
                [DotnetRootX86EnvVarName] = null!,
                [DotnetRootArm64EnvVarName] = null!,
            };
        }

        /// <summary>
        /// Applies environment variable overrides to a dictionary.
        /// A non-null value sets or overrides that variable. A null value removes the variable.
        /// </summary>
        /// <param name="environment">The environment dictionary to modify.</param>
        /// <param name="overrides">The overrides to apply. If null, no changes are made.</param>
        internal static void ApplyEnvironmentOverrides(IDictionary<string, string> environment, IDictionary<string, string>? overrides)
        {
            if (overrides == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> kvp in overrides)
            {
                if (kvp.Value == null)
                {
                    environment.Remove(kvp.Key);
                }
                else
                {
                    environment[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
