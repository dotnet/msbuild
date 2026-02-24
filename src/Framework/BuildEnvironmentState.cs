// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Class to encapsulate state that was stored in BuildEnvironmentHelper.
    /// </summary>
    /// <remarks>
    /// This should be deleted when BuildEnvironmentHelper can be moved into Framework.
    /// </remarks>
    internal static class BuildEnvironmentState
    {
        internal static bool s_runningInVisualStudio = false;
        internal static bool s_runningTests = false;

        /// <summary>
        /// Detects the host environment MSBuild is running in (VS, VSCode, CLI, or custom).
        /// Returns null if no specific host could be determined.
        /// </summary>
        internal static string? GetHostName()
        {
            if (s_runningInVisualStudio)
            {
                return "VS";
            }

            string? msbuildHostName = Environment.GetEnvironmentVariable("MSBUILD_HOST_NAME");
            if (!string.IsNullOrEmpty(msbuildHostName))
            {
                return msbuildHostName;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSCODE_CWD")) || Environment.GetEnvironmentVariable("TERM_PROGRAM") == "vscode")
            {
                return "VSCode";
            }

            return null;
        }
    }
}
