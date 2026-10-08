// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Metadata of files that already exist under the toolset, .NET SDK, and NuGet package roots, read once per build.
/// Validating a cached evaluation stats every file it imported, and every project imports the same hundreds of SDK
/// files while nothing writes to those roots during a build. Under the toolset and .NET SDK roots, missing paths
/// and directories are read once per build as well: every project probes the same few dozen absent import hooks and
/// wildcard import directories. The package root shares only existing files, because restore creates new package
/// folders and files there. The user extensions folder (MSBuildUserExtensionsPath) is shared like the toolset roots:
/// every project probes the same absent import hooks there, and sharing them assumes nothing creates or changes a
/// hook while a build runs. A project that overrides MSBuildUserExtensionsPath probes outside this root, so those
/// paths are read from the file system. Links and paths elsewhere are always read from the file system, because tasks
/// can create or change them between two validations.
/// </summary>
internal sealed class ImmutableFileStatCache
{
    private static readonly Lazy<(string[] Roots, string[] ProbeRoots)> s_defaultRoots = new(ComputeDefaultRoots);

    private readonly ConcurrentDictionary<string, FileDependency> _stats = new(FileUtilities.PathComparer);
    private readonly string[] _roots;
    private readonly string[] _probeRoots;

    internal ImmutableFileStatCache()
        : this(s_defaultRoots.Value.Roots, s_defaultRoots.Value.ProbeRoots)
    {
    }

    /// <param name="roots">Directories whose existing files are not modified while a build runs.</param>
    internal ImmutableFileStatCache(IEnumerable<string> roots)
        : this(roots, [])
    {
    }

    /// <param name="roots">Directories whose existing files are not modified while a build runs.</param>
    /// <param name="probeRoots">
    /// Directories where nothing is created, deleted, or changed while a build runs, so a missing path or a directory
    /// read once stays as it was. They are shared roots as well.
    /// </param>
    internal ImmutableFileStatCache(IEnumerable<string> roots, IEnumerable<string> probeRoots)
    {
        _probeRoots = Normalize(probeRoots);
        _roots = [.. Normalize(roots), .. _probeRoots];
    }

    /// <summary>
    /// Reads the metadata of a recorded path, reusing the result for existing files under the roots and for any
    /// result under the probe roots. Returns false for a symbolic link or junction, like
    /// <see cref="EvaluationInputRecorder.TryStat"/>.
    /// </summary>
    internal bool TryStat(string fullPath, out FileDependency dependency)
    {
        bool underProbeRoot = IsUnderRoot(_probeRoots, fullPath);
        if (!underProbeRoot && !IsUnderRoot(_roots, fullPath))
        {
            return EvaluationInputRecorder.TryStat(fullPath, out dependency);
        }

        if (_stats.TryGetValue(fullPath, out dependency))
        {
            return true;
        }

        if (!EvaluationInputRecorder.TryStat(fullPath, out dependency))
        {
            return false;
        }

        if (underProbeRoot || dependency.Kind == PathKind.File)
        {
            _stats.TryAdd(fullPath, dependency);
        }

        return true;
    }

    /// <summary>
    /// Discards every result. Called when a build starts, so a server or other long-lived host re-reads
    /// SDK and package files that changed between builds, and sees paths that restore or an install created.
    /// </summary>
    internal void NotifyBuildStarted() => _stats.Clear();

    private static string[] Normalize(IEnumerable<string> roots)
    {
        var normalized = new List<string>();
        foreach (string root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                normalized.Add(FileUtilities.EnsureTrailingSlash(Path.GetFullPath(root)));
            }
        }

        return [.. normalized];
    }

    private static bool IsUnderRoot(string[] roots, string fullPath)
    {
        foreach (string root in roots)
        {
            if (fullPath.StartsWith(root, FileUtilities.PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    // The toolset roots and the user extensions path are probe roots. The package root is not: restore creates package folders and files in it.
    internal static (string[] Roots, string[] ProbeRoots) ComputeDefaultRoots()
    {
        var toolsetRoots = new List<string>(3);
        BuildEnvironment environment = BuildEnvironmentHelper.Instance;
        string? toolsDirectory = environment.CurrentMSBuildToolsDirectory;
        if (!string.IsNullOrEmpty(toolsDirectory))
        {
            // The .NET SDK layout is <dotnet root>/sdk/<version>; the root also holds packs and workload manifests.
            DirectoryInfo? sdkDirectory = Directory.GetParent(toolsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            toolsetRoots.Add(
                sdkDirectory?.Parent is not null && string.Equals(sdkDirectory.Name, "sdk", StringComparison.OrdinalIgnoreCase)
                    ? sdkDirectory.Parent.FullName
                    : toolsDirectory);
        }

        if (!string.IsNullOrEmpty(environment.MSBuildExtensionsPath))
        {
            toolsetRoots.Add(environment.MSBuildExtensionsPath);
        }

        string? userExtensionsPath = ComputeUserExtensionsPath(toolsDirectory);
        if (userExtensionsPath is not null)
        {
            toolsetRoots.Add(userExtensionsPath);
        }

        string? packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        string packageRoot = !string.IsNullOrEmpty(packages)
            ? packages
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        return ([.. toolsetRoots, packageRoot], [.. toolsetRoots]);
    }

    // Follows how the MSBuildUserExtensionsPath property is computed (Utilities.GetEnvironmentProperties). If the result
    // differs from the property, the hooks are simply read from the file system instead of shared.
    internal static string? ComputeUserExtensionsPath(string? toolsDirectory)
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = toolsDirectory;
        }

        return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Microsoft", "MSBuild");
    }
}
