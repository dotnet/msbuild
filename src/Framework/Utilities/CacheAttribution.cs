// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Shared;

internal static class CacheAttribution
{
    internal enum Op
    {
        GlobTotal, GlobLookup, GlobValidate, GlobStore, GlobCompute,
        Enumeration, Filter, ListingLookup, ListingValidate, ListingStore,
        Stamp, NativeStamp, MemoWait, MemoHold, JournalOpen, JournalResolve,
        JournalIdentity, JournalWait, JournalHold, JournalCatchUp, JournalQuery,
        JournalRead, Existence, DirectTraversal, ListingHit, ListingMiss, StampMemoHit, StampMemoMiss,
        JournalBatch, JournalSubscribe, JournalGuard, Count
    }

    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("MSBUILDCACHEATTRIBUTION") == "1";
    internal static readonly bool DisableShared = Environment.GetEnvironmentVariable("MSBUILDCACHEDIAGNOSTICMODE") == "off";
    private static readonly string Root = Environment.GetEnvironmentVariable("MSBUILDCACHEATTRIBUTIONROOT") ?? "\0";
    private static readonly AsyncLocal<int> PhaseValue = new();
    private static readonly ThreadLocal<State> States = new(() => new State(), trackAllValues: true);
    private const int Width = (int)Op.Count;

    internal sealed class State
    {
        internal readonly long[] Values = new long[7 * 2 * Width * 5];
        internal long ChildTicks;
        internal int Location;
    }

    internal static PhaseScope Phase(int phase) => new(phase);

    internal readonly struct PhaseScope : IDisposable
    {
        private readonly int _previous;
        internal PhaseScope(int phase)
        {
            _previous = Enabled ? PhaseValue.Value : 0;
            if (Enabled) { PhaseValue.Value = phase; }
        }
        public void Dispose()
        {
            if (Enabled) { PhaseValue.Value = _previous; }
        }
    }

    internal static Scope Measure(Op op, string? path = null) => Enabled ? new(op, path) : default;

    internal static void Count(Op op, long value = 1)
    {
        if (!Enabled) { return; }
        State state = States.Value!;
        int index = ((PhaseValue.Value * 2 + state.Location) * Width + (int)op) * 5;
        state.Values[index] += value;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly State? _state;
        private readonly int _index;
        private readonly int _previousLocation;
        private readonly long _started;
        private readonly long _allocated;
        private readonly long _previousChildTicks;

        internal Scope(Op op, string? path)
        {
            _state = States.Value!;
            _previousLocation = _state.Location;
            if (path is not null)
            {
                _state.Location = path.StartsWith(Root, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
            _index = ((PhaseValue.Value * 2 + _state.Location) * Width + (int)op) * 5;
            _previousChildTicks = _state.ChildTicks;
            _state.ChildTicks = 0;
            _allocated = Allocated();
            _started = Stopwatch.GetTimestamp();
        }

        internal void Units(long value)
        {
            if (_state is not null) { _state.Values[_index + 4] += value; }
        }

        public void Dispose()
        {
            if (_state is null) { return; }
            long elapsed = Stopwatch.GetTimestamp() - _started;
            _state.Values[_index]++;
            _state.Values[_index + 1] += elapsed;
            _state.Values[_index + 2] += elapsed - _state.ChildTicks;
            _state.Values[_index + 3] += Allocated() - _allocated;
            _state.ChildTicks = _previousChildTicks + elapsed;
            _state.Location = _previousLocation;
        }
    }

    private static long Allocated()
    {
#if NET
        return GC.GetAllocatedBytesForCurrentThread();
#else
        return 0;
#endif
    }

    internal static LockScope Lock(object gate, Op wait, Op hold) => new(gate, wait, hold);

    internal readonly struct LockScope : IDisposable
    {
        private readonly object _gate;
        private readonly Scope _hold;
        internal LockScope(object gate, Op wait, Op hold)
        {
            _gate = gate;
            using (Measure(wait)) { Monitor.Enter(gate); }
            _hold = Measure(hold);
        }
        public void Dispose()
        {
            _hold.Dispose();
            Monitor.Exit(_gate);
        }
    }

    internal static object GetSnapshot()
    {
        List<object> rows = [];
        long[] totals = new long[7 * 2 * Width * 5];
        foreach (State state in States.Values)
        {
            for (int i = 0; i < totals.Length; i++) { totals[i] += state.Values[i]; }
        }
        for (int phase = 0; phase < 7; phase++)
        {
            for (int location = 0; location < 2; location++)
            {
                for (int op = 0; op < Width; op++)
                {
                    int i = ((phase * 2 + location) * Width + op) * 5;
                    if (totals[i] == 0) { continue; }
                    rows.Add(new
                    {
                        phase, location = location == 1 ? "workload" : "other",
                        operation = ((Op)op).ToString(), count = totals[i],
                        inclusiveSeconds = totals[i + 1] / (double)Stopwatch.Frequency,
                        exclusiveSeconds = totals[i + 2] / (double)Stopwatch.Frequency,
                        allocatedBytes = totals[i + 3], units = totals[i + 4]
                    });
                }
            }
        }
        return new { enabled = Enabled, disableShared = DisableShared, frequency = Stopwatch.Frequency, rows };
    }
}
