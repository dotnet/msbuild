// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Loader;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// Points MSBuild at a real SDK layout.
/// </summary>
/// <remarks>
/// An application that merely references <c>Microsoft.Build</c> resolves <c>MSBuildExtensionsPath</c> to its own
/// output directory, so SDK-style projects fail with "the SDK 'Microsoft.NET.Sdk' could not be resolved". Setting
/// <c>MSBUILD_EXE_PATH</c> before the build environment is first queried makes MSBuild locate itself in the SDK
/// layout instead, which is the same trick <c>MSBuildLocator</c> uses. Assemblies that MSBuild loads lazily (such
/// as <c>NuGet.Frameworks</c>, needed by <c>$([MSBuild]::GetTargetFrameworkIdentifier(...))</c>) are also resolved
/// from that layout.
/// </remarks>
internal static class MSBuildEnvironment
{
    private static int s_initialized;

    public static void Ensure(string? explicitMSBuildPath = null)
    {
        if (Interlocked.Exchange(ref s_initialized, 1) != 0)
        {
            return;
        }

        string msbuildPath = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : explicitMSBuildPath
                ?? FindBootstrapMSBuild()
                ?? throw new InvalidOperationException(
                    "Could not locate an MSBuild installation. Build the repository first, or pass --msbuild-exe-path <path to MSBuild.dll>.");

        string sdkDirectory = Path.GetDirectoryName(msbuildPath)!;

        Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildPath);
        Environment.SetEnvironmentVariable("MSBuildExtensionsPath", sdkDirectory);
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdkDirectory, "Sdks"));

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(sdkDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }

    /// <summary>
    /// Walks up from the running assembly looking for <c>artifacts/bin/bootstrap/core/sdk/&lt;version&gt;/MSBuild.dll</c>.
    /// </summary>
    private static string? FindBootstrapMSBuild()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string sdkRoot = Path.Combine(directory.FullName, "artifacts", "bin", "bootstrap", "core", "sdk");

            if (!Directory.Exists(sdkRoot))
            {
                continue;
            }

            foreach (string versionDirectory in Directory.EnumerateDirectories(sdkRoot))
            {
                string candidate = Path.Combine(versionDirectory, "MSBuild.dll");

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
