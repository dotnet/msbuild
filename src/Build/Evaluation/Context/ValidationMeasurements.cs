// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Where one validation spent its time and what it touched. Only created when evaluation cache diagnostics are on,
/// used by a single validation, and never read by acceptance logic.
/// </summary>
internal sealed class ValidationMeasurements
{
    private static readonly string[] s_statClassLabels = CreateStatClassLabels();

    internal long StatLoopTicks;
    internal long StatClassificationTicks;
    internal long GlobReplayTicks;
    internal long EnvironmentCheckTicks;
    internal long GlobBookkeepingTicks;
    internal long EnvironmentReads;

    // Stats that reached the file system, by where the path is, what was found, and whether the build stat it before.
    private TimedBuckets? _statClasses;

    // The same stats by how long each took.
    private TimedBuckets? _statLatency;

    internal long RecordedFiles;
    internal long RecordedMissing;
    internal long RecordedDirectories;

    internal long SharedStatHits;
    internal long SharedStatMisses;
    internal long SharedStatNotCacheable;
    internal long LiveStats;

    internal long GlobsRecorded;
    internal long GlobsReplayed;
    internal long GlobsSkipped;
    internal long ReplaysByDirectoryStamp;
    internal long ReplaysByCachedExpansion;
    internal long ReplayMismatches;
    internal long ChangedGlobDirectories;
    internal long DirectoriesComparedToRememberedState;
    internal long DirectoriesQuietSinceRememberedState;
    internal long DirectoriesRemembered;
    internal long DirectoriesTooRecentToRemember;

    internal long DriverLegacy;
    internal long DriverOptimizedCallback;
    internal long DriverOptimizedDirect;
    internal long ReplaysWithoutEntryCache;

    internal long ListedDirectories;
    internal long ListingsSupplied;
    internal long ListedEntries;

    internal void CountRecorded(PathKind kind)
    {
        switch (kind)
        {
            case PathKind.File:
                RecordedFiles++;
                break;
            case PathKind.Directory:
                RecordedDirectories++;
                break;
            default:
                RecordedMissing++;
                break;
        }
    }

    // Classifies one stat that reached the file system. underSharedRoot: the path is under the SDK, toolset or
    // package roots. repeat: an earlier validation in this build already stat the same path.
    internal void CountStat(string path, PathKind kind, bool underSharedRoot, bool repeat, long ticks)
    {
        int region = underSharedRoot ? 0 : IsBuildOutput(path) ? 1 : 2;
        (_statClasses ??= new TimedBuckets(s_statClassLabels)).Add((((region * 3) + (int)kind) * 2) + (repeat ? 1 : 0), ticks);
        (_statLatency ??= new TimedBuckets(LatencyBuckets.Labels)).Add(LatencyBuckets.Index(ticks), ticks);
    }

    private static string[] CreateStatClassLabels()
    {
        string[] regions = ["Toolset", "Output", "Repo"];
        string[] kinds = ["Missing", "File", "Directory"];
        string[] labels = new string[regions.Length * kinds.Length * 2];
        int index = 0;
        foreach (string region in regions)
        {
            foreach (string kind in kinds)
            {
                labels[index++] = $"{region}.{kind}.First";
                labels[index++] = $"{region}.{kind}.Repeat";
            }
        }

        return labels;
    }

    // Heuristic: any obj or bin directory segment in the path marks build output, so a checkout that itself lives
    // under a directory with one of those names reports all of its stats as Output.
    private static bool IsBuildOutput(string path) => ContainsSegment(path, "obj") || ContainsSegment(path, "bin");

    private static bool ContainsSegment(string path, string segment)
    {
        int index = 0;
        while ((index = path.IndexOf(segment, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int end = index + segment.Length;
            if (index > 0
                && (path[index - 1] == '\\' || path[index - 1] == '/')
                && (end == path.Length || path[end] == '\\' || path[end] == '/'))
            {
                return true;
            }

            index = end;
        }

        return false;
    }

    internal void CountDriver(FileMatcherDriver driver)
    {
        switch (driver)
        {
            case FileMatcherDriver.Legacy:
                DriverLegacy++;
                break;
            case FileMatcherDriver.OptimizedCallback:
                DriverOptimizedCallback++;
                break;
            default:
                DriverOptimizedDirect++;
                break;
        }
    }

    /// <summary>Adds the totals of this validation to the build's diagnostics.</summary>
    internal void Publish(EvaluationCacheDiagnostics.Request request)
    {
        // The loop timer also ran while stats were being classified, which the diagnostics do and a normal build does not.
        request.AddTiming(EvaluationCacheDiagnostics.Phase.FileStatLoop, Math.Max(0, StatLoopTicks - StatClassificationTicks));
        if (_statClasses is not null)
        {
            request.AddTiming(EvaluationCacheDiagnostics.Phase.StatClassification, StatClassificationTicks);
        }
        request.AddTiming(EvaluationCacheDiagnostics.Phase.EnvironmentCheck, EnvironmentCheckTicks);
        if (GlobsRecorded > 0)
        {
            request.AddTiming(EvaluationCacheDiagnostics.Phase.GlobBookkeeping, GlobBookkeepingTicks);
        }

        if (GlobsReplayed > 0)
        {
            request.AddTiming(EvaluationCacheDiagnostics.Phase.GlobReplay, GlobReplayTicks);
        }

        if (_statClasses is not null)
        {
            request.AddBuckets("StatClass", _statClasses);
        }

        if (_statLatency is not null)
        {
            request.AddBuckets("StatLatency", _statLatency);
        }

        Add(request, nameof(EnvironmentReads), EnvironmentReads);
        Add(request, nameof(RecordedFiles), RecordedFiles);
        Add(request, nameof(RecordedMissing), RecordedMissing);
        Add(request, nameof(RecordedDirectories), RecordedDirectories);
        Add(request, nameof(SharedStatHits), SharedStatHits);
        Add(request, nameof(SharedStatMisses), SharedStatMisses);
        Add(request, nameof(SharedStatNotCacheable), SharedStatNotCacheable);
        Add(request, nameof(LiveStats), LiveStats);
        Add(request, nameof(GlobsRecorded), GlobsRecorded);
        Add(request, nameof(GlobsReplayed), GlobsReplayed);
        Add(request, nameof(GlobsSkipped), GlobsSkipped);
        Add(request, nameof(ReplaysByDirectoryStamp), ReplaysByDirectoryStamp);
        Add(request, nameof(ReplaysByCachedExpansion), ReplaysByCachedExpansion);
        Add(request, nameof(ReplayMismatches), ReplayMismatches);
        Add(request, nameof(ChangedGlobDirectories), ChangedGlobDirectories);
        Add(request, nameof(DirectoriesComparedToRememberedState), DirectoriesComparedToRememberedState);
        Add(request, nameof(DirectoriesQuietSinceRememberedState), DirectoriesQuietSinceRememberedState);
        Add(request, nameof(DirectoriesRemembered), DirectoriesRemembered);
        Add(request, nameof(DirectoriesTooRecentToRemember), DirectoriesTooRecentToRemember);
        Add(request, nameof(DriverLegacy), DriverLegacy);
        Add(request, nameof(DriverOptimizedCallback), DriverOptimizedCallback);
        Add(request, nameof(DriverOptimizedDirect), DriverOptimizedDirect);
        Add(request, nameof(ReplaysWithoutEntryCache), ReplaysWithoutEntryCache);
        Add(request, nameof(ListedDirectories), ListedDirectories);
        Add(request, nameof(ListingsSupplied), ListingsSupplied);
        Add(request, nameof(ListedEntries), ListedEntries);
    }

    internal static long Now() => Stopwatch.GetTimestamp();

    private static void Add(EvaluationCacheDiagnostics.Request request, string name, long value)
    {
        if (value != 0)
        {
            request.AddCount("ValidationDetail." + name, value);
        }
    }
}
