// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace MSBuild.OrchardCore.Benchmarks;

internal static class DotNetSdkLocator
{
    public static string FindSdkPath()
    {
        string? configuredSdksPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        if (!string.IsNullOrWhiteSpace(configuredSdksPath))
        {
            string normalizedSdksPath = Path.GetFullPath(configuredSdksPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string configuredSdkPath = Directory.GetParent(normalizedSdksPath)?.FullName
                ?? throw new InvalidOperationException($"Invalid MSBuildSDKsPath: {configuredSdksPath}");

            if (!IsSdkPath(configuredSdkPath))
            {
                throw new InvalidOperationException(
                    $"MSBuildSDKsPath does not identify a valid .NET SDK: {configuredSdksPath}");
            }

            return configuredSdkPath;
        }

        string dotNetHostPath = FindDotNetHostPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = dotNetHostPath,
            Arguments = "--version",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start the dotnet host: {dotNetHostPath}");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not determine the selected .NET SDK using '{dotNetHostPath} --version': {error.Trim()}");
        }

        string sdkVersion = output.Trim();
        string dotNetRoot = Path.GetDirectoryName(dotNetHostPath)
            ?? throw new InvalidOperationException($"Could not determine the dotnet root from: {dotNetHostPath}");
        string sdkPath = Path.Combine(dotNetRoot, "sdk", sdkVersion);

        if (!IsSdkPath(sdkPath))
        {
            throw new InvalidOperationException(
                $"The selected .NET SDK directory does not exist or is incomplete: {sdkPath}");
        }

        return sdkPath;
    }

    private static string FindDotNetHostPath()
    {
        string? processPath = Environment.ProcessPath;
        if (processPath is { } currentProcessPath && IsDotNetHost(currentProcessPath))
        {
            return currentProcessPath;
        }

        string? configuredHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (configuredHostPath is { } hostPath && IsDotNetHost(hostPath))
        {
            return Path.GetFullPath(hostPath);
        }

        throw new InvalidOperationException(
            "Could not locate the dotnet host. Run the benchmark with dotnet or set MSBuildSDKsPath.");
    }

    private static bool IsDotNetHost(string path)
        => File.Exists(path) &&
           string.Equals(
               Path.GetFileNameWithoutExtension(path),
               "dotnet",
               StringComparison.OrdinalIgnoreCase);

    private static bool IsSdkPath(string path)
        => File.Exists(Path.Combine(path, "Current", "Microsoft.Common.props")) &&
           File.Exists(Path.Combine(path, "NuGet.targets")) &&
           File.Exists(Path.Combine(path, "Sdks", "Microsoft.NET.Sdk", "Sdk", "Sdk.props"));
}
