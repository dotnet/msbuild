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

        internal const string AzureDevOpsHostName = "Azure DevOps";
        internal const string GitHubActionsHostName = "GitHub Action";

        /// <summary>
        /// Environment variables whose presence indicates an automated environment, in addition to <c>CI</c> and <c>GITHUB_ACTIONS</c> being true.
        /// </summary>
        internal static readonly string[] AutomatedEnvironmentVariables =
        [
            "COPILOT_API_URL",    // GitHub Copilot
            "BUILD_ID",           // Jenkins, Google Cloud Build
            "BUILDKITE",          // Buildkite
            "CIRCLECI",           // CircleCI
            "TEAMCITY_VERSION",   // TeamCity
            "TF_BUILD",           // Azure DevOps
            "APPVEYOR",           // AppVeyor
            "TRAVIS",             // Travis CI
            "GITLAB_CI",          // GitLab CI
            "JENKINS_URL",        // Jenkins
            "BAMBOO_BUILD_NUMBER" // Atlassian Bamboo
        ];

        /// <summary>
        /// Determines if the current environment is an automated environment, such as a CI system, GitHub Actions or GitHub Copilot.
        /// </summary>
        internal static bool IsAutomatedEnvironment()
        {
            if (EnvironmentUtilities.IsValueOneOrTrue("CI") ||
                EnvironmentUtilities.IsValueOneOrTrue("GITHUB_ACTIONS"))
            {
                return true;
            }

            foreach (string variable in AutomatedEnvironmentVariables)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Detects the host environment MSBuild is running in (VS, VSCode, CI, CLI, or custom).
        /// Returns null if no specific host could be determined.
        /// </summary>
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
