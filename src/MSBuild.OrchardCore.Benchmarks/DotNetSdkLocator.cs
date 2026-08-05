// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            if (IsSdkPath(configuredSdkPath))
            {
                return configuredSdkPath;
            }
        }

        var dotNetRoots = new List<string>();
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        AddDotNetRoot(dotNetRoots, seenRoots, Environment.GetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"));
        AddDotNetRoot(dotNetRoots, seenRoots, Environment.GetEnvironmentVariable("DOTNET_ROOT"));
        AddDotNetRoot(dotNetRoots, seenRoots, Path.GetDirectoryName(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")));
        AddDotNetRoot(dotNetRoots, seenRoots, Path.GetDirectoryName(Environment.ProcessPath));

        string runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        AddDotNetRoot(
            dotNetRoots,
            seenRoots,
            Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", "..")));

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string pathEntry in path.Split(Path.PathSeparator))
            {
                string trimmedEntry = pathEntry.Trim();
                if (File.Exists(Path.Combine(trimmedEntry, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")))
                {
                    AddDotNetRoot(dotNetRoots, seenRoots, trimmedEntry);
                }
            }
        }

        foreach (string dotNetRoot in dotNetRoots)
        {
            string? bestSdk = FindBestSdk(dotNetRoot);
            if (bestSdk is not null)
            {
                return bestSdk;
            }
        }

        throw new InvalidOperationException(
            "Could not locate a .NET SDK. Set MSBuildSDKsPath or run the benchmark with dotnet.");
    }

    private static void AddDotNetRoot(List<string> roots, HashSet<string> seenRoots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            string fullPath = Path.GetFullPath(path);
            if (seenRoots.Add(fullPath))
            {
                roots.Add(fullPath);
            }
        }
    }

    private static string? FindBestSdk(string dotNetRoot)
    {
        string sdkRoot = Path.Combine(dotNetRoot, "sdk");
        if (!Directory.Exists(sdkRoot))
        {
            return null;
        }

        string? bestSdk = null;
        foreach (string candidate in Directory.EnumerateDirectories(sdkRoot))
        {
            if (IsSdkPath(candidate) && (bestSdk is null || CompareSdkVersions(candidate, bestSdk) > 0))
            {
                bestSdk = candidate;
            }
        }

        return bestSdk;
    }

    private static int CompareSdkVersions(string leftPath, string rightPath)
    {
        string left = Path.GetFileName(leftPath);
        string right = Path.GetFileName(rightPath);
        string leftNumeric = left.Split('-')[0];
        string rightNumeric = right.Split('-')[0];

        bool leftParsed = Version.TryParse(leftNumeric, out Version? leftVersion);
        bool rightParsed = Version.TryParse(rightNumeric, out Version? rightVersion);
        if (leftParsed && rightParsed)
        {
            int versionComparison = leftVersion!.CompareTo(rightVersion);
            if (versionComparison != 0)
            {
                return versionComparison;
            }

            bool leftIsPrerelease = left.Length != leftNumeric.Length;
            bool rightIsPrerelease = right.Length != rightNumeric.Length;
            if (leftIsPrerelease != rightIsPrerelease)
            {
                return leftIsPrerelease ? -1 : 1;
            }

            if (leftIsPrerelease)
            {
                int prereleaseComparison = ComparePrerelease(
                    left.Substring(leftNumeric.Length + 1),
                    right.Substring(rightNumeric.Length + 1));
                if (prereleaseComparison != 0)
                {
                    return prereleaseComparison;
                }
            }
        }
        else if (leftParsed != rightParsed)
        {
            return leftParsed ? 1 : -1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static int ComparePrerelease(string left, string right)
    {
        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        int commonLength = Math.Min(leftParts.Length, rightParts.Length);
        for (int i = 0; i < commonLength; i++)
        {
            bool leftNumeric = int.TryParse(leftParts[i], out int leftNumber);
            bool rightNumeric = int.TryParse(rightParts[i], out int rightNumber);
            int comparison;
            if (leftNumeric && rightNumeric)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric != rightNumeric)
            {
                comparison = leftNumeric ? -1 : 1;
            }
            else
            {
                comparison = string.CompareOrdinal(leftParts[i], rightParts[i]);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static bool IsSdkPath(string path)
        => File.Exists(Path.Combine(path, "Current", "Microsoft.Common.props")) &&
           File.Exists(Path.Combine(path, "Sdks", "Microsoft.NET.Sdk", "Sdk", "Sdk.props"));
}
