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
/// files while nothing writes to those roots during a build. Missing paths, directories, links, and files elsewhere
/// are always read from the file system, because tasks can create or change them between two validations.
/// </summary>
internal sealed class ImmutableFileStatCache
{
    private static readonly Lazy<string[]> s_defaultRoots = new(ComputeDefaultRoots);

    private readonly ConcurrentDictionary<string, FileDependency> _files = new(FileUtilities.PathComparer);
    private readonly string[] _roots;

    // Paths stat from the file system in the current build; only maintained while diagnostics are on.
    private ConcurrentDictionary<string, bool>? _statedPaths;

    internal ImmutableFileStatCache()
        : this(s_defaultRoots.Value)
    {
    }

    /// <param name="roots">Directories whose existing files are not modified while a build runs.</param>
    internal ImmutableFileStatCache(IEnumerable<string> roots)
    {
        var normalized = new List<string>();
        foreach (string root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                normalized.Add(FileUtilities.EnsureTrailingSlash(Path.GetFullPath(root)));
            }
        }

        _roots = [.. normalized];
    }

    /// <summary>
    /// Reads the metadata of a recorded path, reusing the result for existing files under the roots.
    /// Returns false for a symbolic link or junction, like <see cref="EvaluationInputRecorder.TryStat"/>.
    /// </summary>
    internal bool TryStat(string fullPath, out FileDependency dependency)
    {
        if (!IsUnderRoot(fullPath))
        {
            return EvaluationInputRecorder.TryStat(fullPath, out dependency);
        }

        if (_files.TryGetValue(fullPath, out dependency))
        {
            return true;
        }

        if (!EvaluationInputRecorder.TryStat(fullPath, out dependency))
        {
            return false;
        }

        if (dependency.Kind == PathKind.File)
        {
            _files.TryAdd(fullPath, dependency);
        }

        return true;
    }

    internal bool TryStat(string fullPath, out FileDependency dependency, ValidationMeasurements? measurements)
    {
        if (!IsUnderRoot(fullPath))
        {
            if (measurements is not null)
            {
                measurements.LiveStats++;
            }

            return Stat(fullPath, out dependency, measurements, underSharedRoot: false);
        }

        if (_files.TryGetValue(fullPath, out dependency))
        {
            if (measurements is not null)
            {
                measurements.SharedStatHits++;
            }

            return true;
        }

        if (!Stat(fullPath, out dependency, measurements, underSharedRoot: true))
        {
            if (measurements is not null)
            {
                measurements.SharedStatNotCacheable++;
            }

            return false;
        }

        if (dependency.Kind == PathKind.File)
        {
            _files.TryAdd(fullPath, dependency);
            if (measurements is not null)
            {
                measurements.SharedStatMisses++;
            }
        }
        else if (measurements is not null)
        {
            measurements.SharedStatNotCacheable++;
        }

        return true;
    }

    /// <summary>
    /// Discards every result. Called when a build starts, so a server or other long-lived host re-reads
    /// SDK and package files that changed between builds.
    /// </summary>
    internal void NotifyBuildStarted()
    {
        _files.Clear();
        _statedPaths?.Clear();
    }

    // Reads the file system. With diagnostics on, also classifies the stat and times it, which never changes the result.
    private bool Stat(string fullPath, out FileDependency dependency, ValidationMeasurements? measurements, bool underSharedRoot)
    {
        if (measurements is null)
        {
            return EvaluationInputRecorder.TryStat(fullPath, out dependency);
        }

        long start = ValidationMeasurements.Now();
        bool found = EvaluationInputRecorder.TryStat(fullPath, out dependency);
        long statEnd = ValidationMeasurements.Now();
        if (found)
        {
            bool repeat = !StatedPaths.TryAdd(fullPath, true);
            measurements.CountStat(fullPath, dependency.Kind, underSharedRoot, repeat, statEnd - start);
            measurements.StatClassificationTicks += ValidationMeasurements.Now() - statEnd;
        }

        return found;
    }

    private ConcurrentDictionary<string, bool> StatedPaths
    {
        get
        {
            if (_statedPaths is null)
            {
                System.Threading.Interlocked.CompareExchange(ref _statedPaths, new ConcurrentDictionary<string, bool>(FileUtilities.PathComparer), null);
            }

            return _statedPaths!;
        }
    }

    private bool IsUnderRoot(string fullPath)
    {
        foreach (string root in _roots)
        {
            if (fullPath.StartsWith(root, FileUtilities.PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] ComputeDefaultRoots()
    {
        var roots = new List<string>(3);
        BuildEnvironment environment = BuildEnvironmentHelper.Instance;
        string? toolsDirectory = environment.CurrentMSBuildToolsDirectory;
        if (!string.IsNullOrEmpty(toolsDirectory))
        {
            // The .NET SDK layout is <dotnet root>/sdk/<version>; the root also holds packs and workload manifests.
            DirectoryInfo? sdkDirectory = Directory.GetParent(toolsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            roots.Add(
                sdkDirectory?.Parent is not null && string.Equals(sdkDirectory.Name, "sdk", StringComparison.OrdinalIgnoreCase)
                    ? sdkDirectory.Parent.FullName
                    : toolsDirectory);
        }

        if (!string.IsNullOrEmpty(environment.MSBuildExtensionsPath))
        {
            roots.Add(environment.MSBuildExtensionsPath);
        }

        string? packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        roots.Add(
            !string.IsNullOrEmpty(packages)
                ? packages
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));

        return [.. roots];
    }
}
