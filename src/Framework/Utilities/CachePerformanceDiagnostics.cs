// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Shared;

internal static class CachePerformanceDiagnostics
{
    private static readonly string? Mode = Environment.GetEnvironmentVariable("MSBUILDCACHEDIAGNOSTICS");
    private static readonly bool PerfStarBinlog = Environment.GetEnvironmentVariable("PERFSTAR_DIAG_COLLECT_BINLOG") == "1";
    internal static readonly bool Enabled = Mode is "1" or "profile" || PerfStarBinlog;
    internal static readonly bool ProfileEvaluation = Mode == "profile" || PerfStarBinlog;
    private static readonly AsyncLocal<Session?> CurrentSession = new();
    private static readonly int ProcessId = Enabled ? ReadProcessId() : 0;
    private static int s_nextCacheId;

    internal enum Operation
    {
        Glob,
        LocalGlobHit,
        GlobSharingEnabled,
        SharedGlobLookup,
        SharedGlobHit,
        SharedGlobMiss,
        SharedGlobInvalidated,
        SharedGlobStore,
        GlobRejectedUnshareableListing,
        GlobRejectedDependency,
        GlobRejectedSkippedLink,
        GlobRejectedConflictingDependency,
        GlobRejectedResult,
        GlobCompute,
        GlobValidation,
        GlobListingDependency,
        GlobExistenceDependency,
        RawLocalHit,
        RawLocalMiss,
        SharedListingLookup,
        SharedListingHit,
        SharedListingMiss,
        SharedListingInvalidated,
        Metadata,
        MetadataUnavailable,
        Enumeration,
        EnumeratedEntries,
        EnumerationFailedOrUnknown,
        DirectEnumeration,
        DirectEnumerationFailed,
        DirectDirectoriesCompleted,
        DirectEntriesObserved,
        LegacyDriver,
        CallbackDriver,
        DirectDriver,
        ListingStore,
        Filter,
        DirectoryExists,
        InitialProperties,
        Properties,
        ItemDefinitions,
        Items,
        UsingTasks,
        Targets,
        Count,
    }

    internal static Session? BeginEvaluation()
    {
        if (!Enabled) { return null; }
        Session session = new(CurrentSession.Value);
        CurrentSession.Value = session;
        return session;
    }

    internal static int NextCacheId() => Enabled ? Interlocked.Increment(ref s_nextCacheId) : 0;

    private static int ReadProcessId()
    {
        using Process process = Process.GetCurrentProcess();
        return process.Id;
    }

    internal static void UseCache(DirectoryListingCache cache)
    {
        if (Enabled) { CurrentSession.Value?.UseCache(cache); }
    }

    internal static void Add(Operation operation, long count = 1)
    {
        if (Enabled) { CurrentSession.Value?.Add(operation, count); }
    }

    internal static Measurement Measure(Operation operation)
        => Enabled ? new Measurement(CurrentSession.Value, operation) : default;

    internal static void RecordPass(string pass, double seconds)
    {
        if (!Enabled || double.IsNaN(seconds)) { return; }
        Operation operation = pass switch
        {
            "initial_properties" => Operation.InitialProperties,
            "properties" => Operation.Properties,
            "item_definitions" => Operation.ItemDefinitions,
            "items" => Operation.Items,
            "using_tasks" => Operation.UsingTasks,
            "targets" => Operation.Targets,
            _ => throw new ArgumentOutOfRangeException(nameof(pass)),
        };
        CurrentSession.Value?.RecordPass(operation, (long)(seconds * Stopwatch.Frequency));
    }

    internal readonly struct Measurement : IDisposable
    {
        private readonly Session? _session;
        private readonly Operation _operation;
        private readonly long _start;

        internal Measurement(Session? session, Operation operation)
        {
            _session = session;
            _operation = operation;
            _start = session is not null ? Stopwatch.GetTimestamp() : 0;
        }

        public void Dispose()
        {
            _session?.Record(_operation, Stopwatch.GetTimestamp() - _start);
        }
    }

    internal sealed class Session : IDisposable
    {
        private readonly Session? _previous;
        private readonly long _start = Stopwatch.GetTimestamp();
        private readonly long[] _counts = new long[(int)Operation.Count];
        private readonly long[] _ticks = new long[(int)Operation.Count];
        private readonly long[] _previousCounts = new long[(int)Operation.InitialProperties];
        private readonly long[] _previousTicks = new long[(int)Operation.InitialProperties];
        private readonly Dictionary<Operation, (long[] Counts, long[] Ticks)> _passes = new();
        private readonly int _gc0 = GC.CollectionCount(0);
        private readonly int _gc1 = GC.CollectionCount(1);
        private readonly int _gc2 = GC.CollectionCount(2);
#if NET
        private readonly long _allocated = GC.GetTotalAllocatedBytes(precise: false);
#endif
        private DirectoryListingCache? _cache;
        private int _mixedCaches;

        internal Session(Session? previous) => _previous = previous;

        internal void Add(Operation operation, long count)
            => Interlocked.Add(ref _counts[(int)operation], count);

        internal void Record(Operation operation, long ticks)
        {
            Interlocked.Increment(ref _counts[(int)operation]);
            Interlocked.Add(ref _ticks[(int)operation], ticks);
        }

        internal void RecordPass(Operation pass, long ticks)
        {
            Record(pass, ticks);
            long[] counts = new long[(int)Operation.InitialProperties];
            long[] times = new long[(int)Operation.InitialProperties];
            for (int index = 0; index < counts.Length; index++)
            {
                long count = Volatile.Read(ref _counts[index]);
                long time = Volatile.Read(ref _ticks[index]);
                counts[index] = count - _previousCounts[index];
                times[index] = time - _previousTicks[index];
                _previousCounts[index] = count;
                _previousTicks[index] = time;
            }
            _passes.Add(pass, (counts, times));
        }

        internal void UseCache(DirectoryListingCache cache)
        {
            DirectoryListingCache? first = Interlocked.CompareExchange(ref _cache, cache, null);
            if (first is not null && !ReferenceEquals(first, cache)) { Interlocked.Exchange(ref _mixedCaches, 1); }
        }

        internal string Complete(int evaluationId, bool restoring, bool succeeded)
        {
            long elapsed = Stopwatch.GetTimestamp() - _start;
            int gc0 = GC.CollectionCount(0) - _gc0;
            int gc1 = GC.CollectionCount(1) - _gc1;
            int gc2 = GC.CollectionCount(2) - _gc2;
#if NET
            long allocated = GC.GetTotalAllocatedBytes(precise: false) - _allocated;
#else
            const long allocated = -1;
#endif
            StringBuilder message = new("MSBuildCacheDiagnostics v=1");
            Append("pid", ProcessId);
            Append("evaluation", evaluationId);
            Append("restore", restoring ? 1 : 0);
            Append("succeeded", succeeded ? 1 : 0);
            Append("cache", _cache?.DiagnosticId ?? 0);
            Append("mixedCaches", _mixedCaches);
            message.Append(" strategy=").Append(_cache?.DiagnosticValidationMode ?? "none");
            Append("retainedListings", _cache?.DiagnosticListingCount ?? 0);
            Append("elapsedTicks", elapsed);
            Append("startTimestamp", _start);
            Append("endTimestamp", _start + elapsed);
            Append("frequency", Stopwatch.Frequency);
            Append("gc0ProcessDelta", gc0);
            Append("gc1ProcessDelta", gc1);
            Append("gc2ProcessDelta", gc2);
            Append("allocatedProcessDelta", allocated);
            using Process process = Process.GetCurrentProcess();
            Append("processCpuTicks", process.TotalProcessorTime.Ticks);
            Append("gcHeapBytes", GC.GetTotalMemory(forceFullCollection: false));
            for (int index = 0; index < (int)Operation.Count; index++)
            {
                string name = ((Operation)index).ToString();
                Append(name, Volatile.Read(ref _counts[index]));
                Append(name + "Ticks", Volatile.Read(ref _ticks[index]));
            }
            foreach (var pass in _passes)
            {
                for (int index = 0; index < pass.Value.Counts.Length; index++)
                {
                    if (pass.Value.Counts[index] == 0 && pass.Value.Ticks[index] == 0) { continue; }
                    string name = pass.Key.ToString() + ((Operation)index).ToString();
                    Append(name, pass.Value.Counts[index]);
                    Append(name + "Ticks", pass.Value.Ticks[index]);
                }
            }
            return message.ToString();

            void Append(string name, long value)
                => message.Append(' ').Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Dispose() => CurrentSession.Value = _previous;
    }
}
