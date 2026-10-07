// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
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
    internal long DirectoriesComparedToRememberedState;
    internal long DirectoriesQuietSinceRememberedState;
    internal long DirectoriesRemembered;
    internal long DirectoriesTooRecentToRemember;

    internal long DriverLegacy;
    internal long DriverOptimizedCallback;
    internal long DriverOptimizedDirect;
    internal long ReplaysWithoutEntryCache;

    internal long ListedDirectories;
    internal long ListedEntries;

    // What replaying only the globs that traverse a changed directory would have done. Computed beside the real
    // replay, which still replays every glob of the entry.
    internal long GlobsNeededByAttribution;
    internal long GlobsAvoidableByAttribution;
    internal long GlobReplayTicksAvoidable;
    internal long ReplayDirectoriesAll;
    internal long ReplayDirectoriesNeeded;
    internal long ChangedDirectoriesUnclaimed;
    internal long ChangedDirectoriesObjOrBin;
    internal long ChangedDirectoriesOther;
    internal long GlobsWithoutRecordedDirectories;
    internal long GlobsTraversingObjOrBin;
    internal long RecordedGlobDirectories;
    internal long EntriesReplayed;
    internal long EntriesReplayAvoidableEntirely;

    private HashSet<string>? _changedDirectories;
    private HashSet<string>? _replayedDirectories;
    private HashSet<string>? _neededDirectories;
    private bool _attributionFallsBackToAll;
    private long _entryReplayed;
    private long _entryNeeded;

    /// <summary>Starts the attribution shadow for one entry's globs.</summary>
    internal void BeginAttribution(
        ImmutableArray<GlobDependency> globs,
        List<KeyValuePair<string, FileDependency>>? changedDirectories)
    {
        _changedDirectories = new HashSet<string>(FileUtilities.PathComparer);
        if (changedDirectories is not null)
        {
            foreach (KeyValuePair<string, FileDependency> changed in changedDirectories)
            {
                _changedDirectories.Add(changed.Key);
                if (IsObjOrBin(changed.Key))
                {
                    ChangedDirectoriesObjOrBin++;
                }
                else
                {
                    ChangedDirectoriesOther++;
                }
            }
        }

        var claimed = new HashSet<string>(FileUtilities.PathComparer);
        bool anyWithoutDirectories = false;
        foreach (GlobDependency glob in globs)
        {
            if (glob.TraversedDirectories is null)
            {
                GlobsWithoutRecordedDirectories++;
                anyWithoutDirectories = true;
                continue;
            }

            RecordedGlobDirectories += glob.TraversedDirectories.Length;
            foreach (string directory in glob.TraversedDirectories)
            {
                claimed.Add(directory);
            }
        }

        int unclaimed = 0;
        foreach (string changed in _changedDirectories)
        {
            if (!claimed.Contains(changed))
            {
                unclaimed++;
            }
        }

        ChangedDirectoriesUnclaimed += unclaimed;
        _attributionFallsBackToAll = anyWithoutDirectories || unclaimed > 0;
        _replayedDirectories = new HashSet<string>(FileUtilities.PathComparer);
        _neededDirectories = new HashSet<string>(FileUtilities.PathComparer);
        _entryReplayed = 0;
        _entryNeeded = 0;
    }

    /// <summary>Classifies one replayed glob as needed or avoidable under attribution.</summary>
    internal void ObserveReplayedGlob(GlobDependency glob, long replayTicks)
    {
        string[]? directories = glob.TraversedDirectories;
        bool intersectsChange = false;
        bool traversesObjOrBin = false;
        if (directories is not null)
        {
            foreach (string directory in directories)
            {
                intersectsChange |= _changedDirectories!.Contains(directory);
                traversesObjOrBin |= IsObjOrBin(directory);
                _replayedDirectories!.Add(directory);
            }
        }

        if (traversesObjOrBin)
        {
            GlobsTraversingObjOrBin++;
        }

        _entryReplayed++;
        if (glob.FromCache || _attributionFallsBackToAll || intersectsChange)
        {
            GlobsNeededByAttribution++;
            _entryNeeded++;
            if (directories is not null)
            {
                foreach (string directory in directories)
                {
                    _neededDirectories!.Add(directory);
                }
            }
        }
        else
        {
            GlobsAvoidableByAttribution++;
            GlobReplayTicksAvoidable += replayTicks;
        }
    }

    internal void EndAttribution()
    {
        if (_replayedDirectories is null)
        {
            return;
        }

        if (_entryReplayed > 0)
        {
            EntriesReplayed++;
            if (_entryNeeded == 0)
            {
                EntriesReplayAvoidableEntirely++;
            }
        }

        ReplayDirectoriesAll += _replayedDirectories.Count;
        ReplayDirectoriesNeeded += _neededDirectories!.Count;
        _replayedDirectories = null;
    }

    private static bool IsObjOrBin(string path) => ContainsSegment(path, "obj") || ContainsSegment(path, "bin");

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
        Add(request, nameof(DirectoriesComparedToRememberedState), DirectoriesComparedToRememberedState);
        Add(request, nameof(DirectoriesQuietSinceRememberedState), DirectoriesQuietSinceRememberedState);
        Add(request, nameof(DirectoriesRemembered), DirectoriesRemembered);
        Add(request, nameof(DirectoriesTooRecentToRemember), DirectoriesTooRecentToRemember);
        Add(request, nameof(DriverLegacy), DriverLegacy);
        Add(request, nameof(DriverOptimizedCallback), DriverOptimizedCallback);
        Add(request, nameof(DriverOptimizedDirect), DriverOptimizedDirect);
        Add(request, nameof(ReplaysWithoutEntryCache), ReplaysWithoutEntryCache);
        Add(request, nameof(ListedDirectories), ListedDirectories);
        Add(request, nameof(ListedEntries), ListedEntries);
        Add(request, nameof(GlobsNeededByAttribution), GlobsNeededByAttribution);
        Add(request, nameof(GlobsAvoidableByAttribution), GlobsAvoidableByAttribution);
        Add(request, "GlobReplayMicrosAvoidable", GlobReplayTicksAvoidable * 1_000_000 / Stopwatch.Frequency);
        Add(request, nameof(ReplayDirectoriesAll), ReplayDirectoriesAll);
        Add(request, nameof(ReplayDirectoriesNeeded), ReplayDirectoriesNeeded);
        Add(request, nameof(ChangedDirectoriesUnclaimed), ChangedDirectoriesUnclaimed);
        Add(request, nameof(ChangedDirectoriesObjOrBin), ChangedDirectoriesObjOrBin);
        Add(request, nameof(ChangedDirectoriesOther), ChangedDirectoriesOther);
        Add(request, nameof(GlobsWithoutRecordedDirectories), GlobsWithoutRecordedDirectories);
        Add(request, nameof(GlobsTraversingObjOrBin), GlobsTraversingObjOrBin);
        Add(request, nameof(RecordedGlobDirectories), RecordedGlobDirectories);
        Add(request, nameof(EntriesReplayed), EntriesReplayed);
        Add(request, nameof(EntriesReplayAvoidableEntirely), EntriesReplayAvoidableEntirely);
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
