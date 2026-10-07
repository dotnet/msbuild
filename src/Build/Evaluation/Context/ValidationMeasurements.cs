// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    internal long StatLoopTicks;
    internal long GlobReplayTicks;

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

    internal long DriverLegacy;
    internal long DriverOptimizedCallback;
    internal long DriverOptimizedDirect;
    internal long ReplaysWithoutEntryCache;

    internal long ListedDirectories;
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
        request.AddTiming(EvaluationCacheDiagnostics.Phase.FileStatLoop, StatLoopTicks);
        if (GlobsReplayed > 0)
        {
            request.AddTiming(EvaluationCacheDiagnostics.Phase.GlobReplay, GlobReplayTicks);
        }

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
        Add(request, nameof(DriverLegacy), DriverLegacy);
        Add(request, nameof(DriverOptimizedCallback), DriverOptimizedCallback);
        Add(request, nameof(DriverOptimizedDirect), DriverOptimizedDirect);
        Add(request, nameof(ReplaysWithoutEntryCache), ReplaysWithoutEntryCache);
        Add(request, nameof(ListedDirectories), ListedDirectories);
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
