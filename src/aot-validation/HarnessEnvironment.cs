// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Provides the MSBuild toolset location the object model needs before any of its types initialize.
///
/// FINDING (the reason this exists): in a Native AOT / single-file executable there is no
/// <c>MSBuild.dll</c> on disk next to the app and <see cref="System.Reflection.Assembly.Location"/>
/// returns an empty string, so <c>BuildEnvironmentHelper</c> cannot discover a toolset on its own and
/// <c>new ProjectCollection()</c> throws <c>ArgumentException: The path is empty</c>. A real AOT host
/// (the dotnet CLI) already knows where its SDK/MSBuild lives and points the engine at it via the
/// <c>MSBUILD_EXE_PATH</c> environment variable (the SDK does exactly this today). This harness mirrors
/// that contract by pointing at the repository's bootstrap toolset before the first object-model access.
/// </summary>
internal static class HarnessEnvironment
{
    [ModuleInitializer]
    internal static void EnsureMSBuildToolset()
    {
        // Respect an externally provided toolset (for example a real SDK layout in CI).
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH")))
        {
            return;
        }

        string? toolset = FindBootstrapMSBuild(AppContext.BaseDirectory);
        if (toolset is not null)
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", toolset);
        }
    }

    /// <summary>
    /// Walks up from the executable directory to the repository root and returns the bootstrap
    /// <c>MSBuild.dll</c> (a complete toolset produced by <c>build.cmd</c>), or null if not found.
    /// </summary>
    private static string? FindBootstrapMSBuild(string startDirectory)
    {
        for (DirectoryInfo? dir = new(startDirectory); dir is not null; dir = dir.Parent)
        {
            string sdkRoot = Path.Combine(dir.FullName, "artifacts", "bin", "bootstrap", "core", "sdk");
            if (Directory.Exists(sdkRoot))
            {
                foreach (string sdkVersionDir in Directory.EnumerateDirectories(sdkRoot))
                {
                    string candidate = Path.Combine(sdkVersionDir, "MSBuild.dll");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }
}
