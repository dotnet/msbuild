// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal static class PropertyPhaseMetrics
{
    private static readonly bool s_enabled = Environment.GetEnvironmentVariable("MSBUILD_TEMP_PROPERTY_PHASE_METRICS") == "1";

    [ThreadStatic]
    private static Context? t_current;

    internal static readonly string[] PassNames =
    [
        "properties_detail_other",
        "properties_detail_traversal",
        "properties_detail_elements",
        "properties_detail_expansion",
        "properties_detail_assignment",
        "properties_detail_condition_acquisition",
        "properties_detail_condition_parsing",
        "properties_detail_condition_state",
        "properties_detail_condition_execution",
        "properties_detail_condition_cleanup",
        "properties_detail_imports",
        "properties_detail_import_expansion",
        "properties_detail_import_search",
        "properties_detail_project_load",
        "properties_detail_sdk_resolution",
        "properties_detail_finalization",
    ];

    internal enum Bucket
    {
        Other,
        Traversal,
        Elements,
        Expansion,
        Assignment,
        ConditionAcquisition,
        ConditionParsing,
        ConditionState,
        ConditionExecution,
        ConditionCleanup,
        Imports,
        ImportExpansion,
        ImportSearch,
        ProjectLoad,
        SdkResolution,
        Finalization,
    }

    internal static Context? Create() => s_enabled ? new Context() : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Scope Enter(Bucket bucket)
    {
        Context? context = t_current;
        if (context is null)
        {
            return default;
        }

        Bucket previous = context._bucket;
        context.Change(bucket);
        return new Scope(context, previous);
    }

    internal readonly struct Scope(Context? context, Bucket previous) : IDisposable
    {
        public void Dispose() => context?.Change(previous);
    }

    internal sealed class Context : IDisposable
    {
        private readonly long[] _ticks = new long[PassNames.Length];
        private Context? _parent;
        private long _start;
        private long _last;
        private bool _active;
        internal Bucket _bucket;

        internal long TotalTicks { get; private set; }

        internal ReadOnlySpan<long> Ticks => _ticks;

        internal double GetSeconds(int index) => _ticks[index] / (double)Stopwatch.Frequency;

        internal double TotalSeconds => TotalTicks / (double)Stopwatch.Frequency;

        internal void Start()
        {
            _parent = t_current;
            t_current = this;
            _active = true;
            _start = _last = Stopwatch.GetTimestamp();
        }

        internal void Change(Bucket bucket)
        {
            long now = Stopwatch.GetTimestamp();
            _ticks[(int)_bucket] += now - _last;
            _last = now;
            _bucket = bucket;
        }

        internal void Stop()
        {
            if (!_active)
            {
                return;
            }

            Change(Bucket.Other);
            TotalTicks = _last - _start;
            t_current = _parent;
            _active = false;
        }

        public void Dispose() => Stop();
    }
}
