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
        internal const string GenericCIHostName = "CI";

        /// <summary>
        /// Environment variables whose presence identifies a specific automated environment, in precedence order.
        /// </summary>
        internal static readonly (string Variable, string HostName)[] CIHostVariables =
        [
            ("TF_BUILD", AzureDevOpsHostName),
            ("COPILOT_API_URL", "GitHub Copilot"),
            ("BUILDKITE", "Buildkite"),
            ("CIRCLECI", "CircleCI"),
            ("TEAMCITY_VERSION", "TeamCity"),
            ("APPVEYOR", "AppVeyor"),
            ("TRAVIS", "Travis CI"),
            ("GITLAB_CI", "GitLab CI"),
            ("JENKINS_URL", "Jenkins"),
            ("BAMBOO_BUILD_NUMBER", "Bamboo"),
        ];

        /// <summary>
        /// Determines if the current environment is an automated environment, such as a CI system, GitHub Actions or GitHub Copilot.
        /// </summary>
        internal static bool IsAutomatedEnvironment() => GetCIHostName() is not null;

        /// <summary>
        /// Returns the name of the automated environment, <see cref="GenericCIHostName"/> if only a generic CI marker
        /// (<c>CI</c> or <c>BUILD_ID</c>) is set, or null outside of an automated environment.
        /// </summary>
        internal static string? GetCIHostName()
        {
            foreach ((string variable, string hostName) in CIHostVariables)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                {
                    return hostName;
                }
            }

            if (EnvironmentUtilities.IsValueOneOrTrue("GITHUB_ACTIONS"))
            {
                return GitHubActionsHostName;
            }

            if (EnvironmentUtilities.IsValueOneOrTrue("CI") ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_ID")))
            {
                return GenericCIHostName;
            }

            return null;
        }

        /// <summary>
        /// Detects the host environment MSBuild is running in: VS, then MSBUILD_HOST_NAME, then <see cref="GetCIHostName"/>, then VS Code.
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

                string? ciHostName = GetCIHostName();
                if (ciHostName is not null)
                {
                    return ciHostName;
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
