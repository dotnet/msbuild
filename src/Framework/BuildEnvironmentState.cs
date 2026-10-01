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
        /// Host name reported when running in an Azure Pipelines job (<c>TF_BUILD</c> is true).
        /// </summary>
        internal const string AzureDevOpsHostName = "Azure DevOps";

        /// <summary>
        /// Host name reported when running in a GitHub Actions job (<c>GITHUB_ACTIONS</c> is true).
        /// </summary>
        internal const string GitHubActionsHostName = "GitHub Action";

        /// <summary>
        /// Detects the host environment MSBuild is running in (VS, VSCode, CI, CLI, or custom).
        /// Returns null if no specific host could be determined.
        /// </summary>
        /// <remarks>
        /// The first matching rule wins:
        /// <list type="number">
        ///   <item>Running inside Visual Studio: <c>"VS"</c>.</item>
        ///   <item>An explicit, non-empty <c>MSBUILD_HOST_NAME</c> (for example set by the .NET CLI or another host): its value.</item>
        ///   <item><c>TF_BUILD</c> is true: <c>"Azure DevOps"</c>.</item>
        ///   <item><c>GITHUB_ACTIONS</c> is true: <c>"GitHub Action"</c>.</item>
        ///   <item><c>VSCODE_CWD</c> is set or <c>TERM_PROGRAM</c> is <c>vscode</c>: <c>"VSCode"</c>.</item>
        /// </list>
        /// CI agents are checked before VS Code because a CI job is the more specific host when both are detected.
        /// </remarks>
        internal static string? GetHostName()
        {
            if (s_runningInVisualStudio)
            {
                return "VS";
            }

            try
            {
                string? msbuildHostName = Environment.GetEnvironmentVariable("MSBUILD_HOST_NAME");
                if (!string.IsNullOrEmpty(msbuildHostName))
                {
                    return msbuildHostName;
                }

                if (EnvironmentUtilities.IsValueOneOrTrue("TF_BUILD"))
                {
                    return AzureDevOpsHostName;
                }

                if (EnvironmentUtilities.IsValueOneOrTrue("GITHUB_ACTIONS"))
                {
                    return GitHubActionsHostName;
                }

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSCODE_CWD"))
                    || string.Equals(Environment.GetEnvironmentVariable("TERM_PROGRAM"), "vscode", StringComparison.OrdinalIgnoreCase))
                {
                    return "VSCode";
                }
            }
            catch
            {
                // Host detection is best-effort; ignore environment access failures.
            }

            return null;
        }
    }
}
