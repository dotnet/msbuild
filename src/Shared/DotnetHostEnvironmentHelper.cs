// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
#if NET
using System.Runtime.InteropServices;
#endif
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
        // Environment variable name for .NET runtime root directory.
        private const string DotnetRootEnvVarName = "DOTNET_ROOT";

#if NET
        // Architecture-specific DOTNET_ROOT environment variable names, dynamically generated
        // to match the native implementation and cover all architectures supported by the runtime.
        private static readonly string[] _archSpecificRootVars = InitializeArchSpecificRootVars();

        private static string[] InitializeArchSpecificRootVars()
        {
            var names = Enum.GetNames<Architecture>();
            var result = new string[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                result[i] = $"DOTNET_ROOT_{names[i].ToUpperInvariant()}";
            }

            return result;
        }
#else
        // On .NET Framework, Architecture enum doesn't exist, so we use hardcoded values.
        // This is sufficient since .NET Framework only runs on Windows x86/x64.
        private static readonly string[] _archSpecificRootVars =
        [
            "DOTNET_ROOT_X86",
            "DOTNET_ROOT_X64",
            "DOTNET_ROOT_ARM64",
        ];
#endif

        /// <summary>
        /// Clears DOTNET_ROOT environment variables that were set only for app host bootstrap.
        /// These should not leak to tools executed by worker nodes.
        /// Only clears if the variable was NOT present in the original build process environment.
        /// </summary>
        /// <param name="buildProcessEnvironment">The original environment from the entry-point process.</param>
        internal static void ClearBootstrapDotnetRootEnvironment(IDictionary<string, string> buildProcessEnvironment)
        {
            if (!buildProcessEnvironment.ContainsKey(DotnetRootEnvVarName))
            {
                Environment.SetEnvironmentVariable(DotnetRootEnvVarName, null);
            }

            foreach (string varName in _archSpecificRootVars)
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

            string? dotnetRoot = null;

            if (!string.IsNullOrEmpty(dotnetHostPath))
            {
                dotnetRoot = Path.GetDirectoryName(dotnetHostPath);
            }
#if RUNTIME_TYPE_NETCORE && BUILD_ENGINE
            else
            {
                // DOTNET_HOST_PATH not set - use CurrentHost to find the dotnet executable.
                string? currentHost = BackEnd.CurrentHost.GetCurrentHost();
                if (!string.IsNullOrEmpty(currentHost))
                {
                    dotnetRoot = Path.GetDirectoryName(currentHost);
                }
            }
#endif

            if (string.IsNullOrEmpty(dotnetRoot))
            {
                if (throwIfNotSet)
                {
                    throw new InvalidOperationException(ResourceUtilities.GetResourceString("DotnetHostPathNotSet"));
                }

                // Could not determine DOTNET_ROOT - no overrides needed
                return null;
            }

            var overrides = new Dictionary<string, string>
            {
                [DotnetRootEnvVarName] = dotnetRoot!,
            };

            // Clear architecture-specific overrides that would take precedence over DOTNET_ROOT
            foreach (string varName in _archSpecificRootVars)
            {
                overrides[varName] = null!;
            }

            return overrides;
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
