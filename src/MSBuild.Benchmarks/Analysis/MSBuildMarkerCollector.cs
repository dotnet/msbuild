// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// Inclusive/exclusive time and invocation count for one MSBuild event source scope.
/// </summary>
internal sealed class ScopeStats
{
    private static readonly double TicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    private long _inclusiveTicks;
    private long _exclusiveTicks;
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public TimeSpan Inclusive => TimeSpan.FromTicks((long)(Interlocked.Read(ref _inclusiveTicks) * TicksPerStopwatchTick));

    public TimeSpan Exclusive => TimeSpan.FromTicks((long)(Interlocked.Read(ref _exclusiveTicks) * TicksPerStopwatchTick));

    public void Add(long inclusiveTicks, long exclusiveTicks)
    {
        Interlocked.Add(ref _inclusiveTicks, inclusiveTicks);
        Interlocked.Add(ref _exclusiveTicks, exclusiveTicks);
        Interlocked.Increment(ref _count);
    }
}

/// <summary>
/// Listens to the <c>Microsoft-Build</c> event source in-process and turns its <c>*Start</c>/<c>*Stop</c> event
/// pairs into a wall-clock breakdown with inclusive and exclusive times.
/// </summary>
/// <remarks>
/// <para>
/// This is the only mechanism that attributes wall-clock time (including time blocked on I/O) to MSBuild's own
/// phases without needing an external trace collector or elevation. It is not free: enabling the event source makes
/// every marker call go through the <see cref="EventSource"/> write path. Use
/// <see cref="MeasuredOverhead"/> together with an uninstrumented run to quantify the distortion.
/// </para>
/// <para>
/// Evaluation of a single project is single threaded, so per-thread scope stacks are sufficient to reconstruct
/// nesting.
/// </para>
/// </remarks>
internal sealed class MSBuildMarkerCollector : EventListener
{
    private const string EventSourceName = "Microsoft-Build";

    /// <summary>
    /// <c>Keywords.All</c> on <c>MSBuildEventSource</c>. Every MSBuild event declares it.
    /// </summary>
    private const EventKeywords AllKeywords = (EventKeywords)0x1;

    [ThreadStatic]
    private static List<Frame>? t_stack;

    private readonly ConcurrentDictionary<string, ScopeStats> _scopes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ScopeStats> _detail = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _unpairedEvents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _detailScopes;
    private readonly List<EventSource> _enabledSources = [];

    private long _eventCount;
    private volatile bool _collecting;

    /// <param name="detailScopes">
    /// Scope names (without the <c>Start</c>/<c>Stop</c> suffix) for which a per-payload breakdown should also be
    /// recorded, for example <c>LoadDocument</c> to see which files are the most expensive to read.
    /// </param>
    public MSBuildMarkerCollector(IEnumerable<string>? detailScopes = null)
        => _detailScopes = new HashSet<string>(detailScopes ?? [], StringComparer.Ordinal);

    /// <summary>Aggregated stats keyed by scope name (the event name minus its <c>Start</c>/<c>Stop</c> suffix).</summary>
    public IReadOnlyDictionary<string, ScopeStats> Scopes => _scopes;

    /// <summary>Aggregated stats keyed by <c>scope|payload</c> for the scopes named in the constructor.</summary>
    public IReadOnlyDictionary<string, ScopeStats> Detail => _detail;

    /// <summary>Events whose <c>Start</c> or <c>Stop</c> partner was never seen. A non-empty result invalidates nesting.</summary>
    public IReadOnlyDictionary<string, long> UnpairedEvents => _unpairedEvents;

    /// <summary>Total number of event source callbacks handled, useful for estimating listener overhead.</summary>
    public long EventCount => Interlocked.Read(ref _eventCount);

    /// <summary>Wall-clock time spent inside this listener's callbacks.</summary>
    public TimeSpan MeasuredOverhead { get; private set; }

    private long _overheadTicks;

    public void Start() => _collecting = true;

    public void Stop()
    {
        _collecting = false;
        MeasuredOverhead = TimeSpan.FromTicks((long)(Interlocked.Read(ref _overheadTicks) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
    }

    public void Reset()
    {
        _scopes.Clear();
        _detail.Clear();
        _unpairedEvents.Clear();
        Interlocked.Exchange(ref _eventCount, 0);
        Interlocked.Exchange(ref _overheadTicks, 0);
        t_stack?.Clear();
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == EventSourceName)
        {
            EnableEvents(eventSource, EventLevel.LogAlways, AllKeywords);
            lock (_enabledSources)
            {
                _enabledSources.Add(eventSource);
            }
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!_collecting)
        {
            return;
        }

        long callbackStart = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _eventCount);

        string? name = eventData.EventName;
        if (name is not null)
        {
            if (name.EndsWith("Start", StringComparison.Ordinal))
            {
                Push(name[..^5], FirstStringPayload(eventData), callbackStart);
            }
            else if (name.EndsWith("Stop", StringComparison.Ordinal))
            {
                Pop(name[..^4], FirstStringPayload(eventData), callbackStart);
            }
            else
            {
                _unpairedEvents.AddOrUpdate(name, 1, static (_, count) => count + 1);
            }
        }

        Interlocked.Add(ref _overheadTicks, Stopwatch.GetTimestamp() - callbackStart);
    }

    private static string? FirstStringPayload(EventWrittenEventArgs eventData)
        => eventData.Payload is { Count: > 0 } payload ? payload[0] as string : null;

    private void Push(string scope, string? payload, long timestamp)
    {
        List<Frame> stack = t_stack ??= [];
        stack.Add(new Frame(scope, payload, timestamp));
    }

    private void Pop(string scope, string? stopPayload, long timestamp)
    {
        List<Frame> stack = t_stack ??= [];

        // Find the innermost matching frame. Scanning (rather than assuming the top of the stack) keeps the
        // reconstruction correct if a scope was left via an exception and never emitted its Stop event.
        int index = -1;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (string.Equals(stack[i].Scope, scope, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            _unpairedEvents.AddOrUpdate(scope + "Stop", 1, static (_, count) => count + 1);
            return;
        }

        // Any frames above the match never stopped; record them as unpaired and discard.
        for (int i = stack.Count - 1; i > index; i--)
        {
            _unpairedEvents.AddOrUpdate(stack[i].Scope + "Start", 1, static (_, count) => count + 1);
            stack.RemoveAt(i);
        }

        Frame frame = stack[index];
        stack.RemoveAt(index);

        long inclusive = timestamp - frame.Start;
        long exclusive = inclusive - frame.ChildTicks;

        _scopes.GetOrAdd(scope, static _ => new ScopeStats()).Add(inclusive, exclusive);

        // Some scopes only carry an identifying payload on their Stop event (SDK resolution names the resolver
        // that answered, for example), so fall back to it when the Start event had nothing useful.
        string? payload = frame.Payload ?? stopPayload;

        if (payload is not null && _detailScopes.Contains(scope))
        {
            _detail.GetOrAdd($"{scope}|{payload}", static _ => new ScopeStats()).Add(inclusive, exclusive);
        }

        if (index > 0)
        {
            Frame parent = stack[index - 1];
            parent.ChildTicks += inclusive;
            stack[index - 1] = parent;
        }
    }

    public override void Dispose()
    {
        lock (_enabledSources)
        {
            foreach (EventSource source in _enabledSources)
            {
                DisableEvents(source);
            }

            _enabledSources.Clear();
        }

        base.Dispose();
    }

    private struct Frame(string scope, string? payload, long start)
    {
        public readonly string Scope = scope;
        public readonly string? Payload = payload;
        public readonly long Start = start;
        public long ChildTicks = 0;
    }
}
