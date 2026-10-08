// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
    private const int MaximumSlowStatsPerValidation = 256;
    private const int BurstSliceMilliseconds = 50;
    private const int BurstSlices = 40;

    private static readonly string[] s_statClassLabels = CreateStatClassLabels();
    private static readonly string[] s_inFlightLatencyLabels = CreateInFlightLatencyLabels();
    private static readonly string[] s_burstLabels = CreateBurstLabels();
    private static readonly string[] s_gcLabels = ["Slow.GcOverlap", "Slow.NoGc", "All.GcOverlap", "All.NoGc"];

    // Stats that took at least this long are reported by path.
    private static readonly long s_slowStatTicks = 2_500 * Stopwatch.Frequency / 1_000_000;

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

    // The same stats by how many were in flight on the shared stat cache when each started, and how long each took.
    private TimedBuckets? _statLatencyByInFlight;

    // The same stats by when each started, in slices since the build's first real stat; all stats, then only slow ones.
    private TimedBuckets? _statBurstAll;
    private TimedBuckets? _statBurstSlow;

    // The same stats by whether a collection finished while each ran; slow stats, then all stats.
    private TimedBuckets? _statGc;

    private List<(string Path, long Ticks)>? _slowStats;

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

    // Adds what the shared stat cache knew when one stat ran. inFlight counts this stat. startOffsetTicks is
    // measured from the build's first real stat. gcOverlapped: a collection finished while the stat ran.
    internal void CountStatContext(string path, int inFlight, long startOffsetTicks, bool gcOverlapped, long ticks)
    {
        int band = inFlight switch
        {
            <= 1 => 0,
            <= 2 => 1,
            <= 4 => 2,
            <= 8 => 3,
            <= 16 => 4,
            <= 32 => 5,
            _ => 6,
        };
        (_statLatencyByInFlight ??= new TimedBuckets(s_inFlightLatencyLabels)).Add((band * LatencyBuckets.Labels.Length) + LatencyBuckets.Index(ticks), ticks);

        long offsetMilliseconds = Math.Max(0, startOffsetTicks) * 1000 / Stopwatch.Frequency;
        int slice = (int)Math.Min(BurstSlices, offsetMilliseconds / BurstSliceMilliseconds);
        (_statBurstAll ??= new TimedBuckets(s_burstLabels)).Add(slice, ticks);

        TimedBuckets gc = _statGc ??= new TimedBuckets(s_gcLabels);
        int collected = gcOverlapped ? 0 : 1;
        gc.Add(2 + collected, ticks);
        if (ticks >= s_slowStatTicks)
        {
            gc.Add(collected, ticks);
            (_statBurstSlow ??= new TimedBuckets(s_burstLabels)).Add(slice, ticks);
            if ((_slowStats ??= []).Count < MaximumSlowStatsPerValidation)
            {
                _slowStats.Add((path, ticks));
            }
        }
    }

    private static string[] CreateInFlightLatencyLabels()
    {
        string[] bands = ["Fl1", "Fl2", "Fl4", "Fl8", "Fl16", "Fl32", "FlGt32"];
        string[] labels = new string[bands.Length * LatencyBuckets.Labels.Length];
        int index = 0;
        foreach (string band in bands)
        {
            foreach (string latency in LatencyBuckets.Labels)
            {
                labels[index++] = $"{band}.{latency}";
            }
        }

        return labels;
    }

    private static string[] CreateBurstLabels()
    {
        string[] labels = new string[BurstSlices + 1];
        for (int i = 0; i < BurstSlices; i++)
        {
            labels[i] = $"T{i * BurstSliceMilliseconds:D4}ms";
        }

        labels[BurstSlices] = $"Gt{BurstSlices * BurstSliceMilliseconds}ms";
        return labels;
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

        if (_statLatencyByInFlight is not null)
        {
            request.AddBuckets("StatLatencyByInFlight", _statLatencyByInFlight);
        }

        if (_statBurstAll is not null)
        {
            request.AddBuckets("StatBurstAll", _statBurstAll);
        }

        if (_statBurstSlow is not null)
        {
            request.AddBuckets("StatBurstSlow", _statBurstSlow);
        }

        if (_statGc is not null)
        {
            request.AddBuckets("StatGc", _statGc);
        }

        if (_slowStats is not null)
        {
            request.AddSlowStats(_slowStats);
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
