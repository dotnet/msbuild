// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Detects whether the process runs on a continuous integration (CI) agent.
/// </summary>
/// <remarks>
/// The rules mirror the .NET SDK's <c>CIEnvironmentDetectorForTelemetry</c> so that MSBuild.exe and the
/// .NET CLI classify the same environments as CI. Detection only selects how already-collected telemetry is
/// delivered when the process exits; it never changes telemetry consent.
/// </remarks>
internal static class CIEnvironmentDetector
{
    // Systems that expose a boolean flag.
    private static readonly string[] s_booleanVariables =
    [
        "TF_BUILD",       // Azure Pipelines
        "GITHUB_ACTIONS", // GitHub Actions
        "APPVEYOR",       // AppVeyor
        "CI",             // General-purpose flag set by most CI systems (Azure Pipelines, GitHub Actions, GitLab, Travis CI, CircleCI, ...)
        "TRAVIS",         // Travis CI
        "CIRCLECI",       // CircleCI
    ];

    // Systems where every variable of a group must be present.
    private static readonly string[][] s_allPresentVariables =
    [
        ["CODEBUILD_BUILD_ID", "AWS_REGION"], // AWS CodeBuild
        ["BUILD_ID", "BUILD_URL"],            // Jenkins
        ["BUILD_ID", "PROJECT_ID"],           // Google Cloud Build
    ];

    // Systems where a single variable being present is sufficient.
    private static readonly string[] s_anyPresentVariables =
    [
        "TEAMCITY_VERSION", // TeamCity
        "JB_SPACE_API_URL", // JetBrains Space
    ];

    /// <summary>
    /// Returns true when the environment identifies a known CI system.
    /// </summary>
    public static bool IsCIEnvironment()
    {
        try
        {
            foreach (string variable in s_booleanVariables)
            {
                if (EnvironmentUtilities.IsValueOneOrTrue(variable))
                {
                    return true;
                }
            }

            foreach (string[] group in s_allPresentVariables)
            {
                bool allPresent = true;
                foreach (string variable in group)
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                    {
                        allPresent = false;
                        break;
                    }
                }

                if (allPresent)
                {
                    return true;
                }
            }

            foreach (string variable in s_anyPresentVariables)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            // Detection is best effort; an inaccessible environment is treated as non-CI.
        }

        return false;
    }
}
