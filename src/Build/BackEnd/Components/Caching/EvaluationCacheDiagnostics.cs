// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Bounded, opt-in decision and phase tracing. This state never participates in cache acceptance or admission.
/// </summary>
internal sealed class EvaluationCacheDiagnostics
{
    internal const string EnvironmentVariable = Traits.EvaluationCacheDiagnosticsEnvVarName;
    internal const int MaximumHistoryEntries = 2048;
    internal const int MaximumEventsPerBuild = 10000;
    internal const int MaximumExamplesPerReason = 3;
    internal const int MaximumExamplesPerBuild = 96;
    internal const int MaximumExampleFieldLength = 512;
    internal const int MaximumSlowPaths = 4096;
    internal const int ReportedSlowPaths = 40;
    internal const int ReportedSlowDirectories = 25;

    internal enum Phase
    {
        RequestKey,
        CacheLookup,
        Validation,
        Materialization,
        FreshEvaluation,
        RestoreEvaluation,
        SnapshotCreation,
        CacheAdmission,
        FallbackPreparation,
        ManifestValidation,
        SdkValidation,
        FileStatLoop,
        GlobReplay,
        RootElementCheck,
        KeyCheck,
        DiagnosticsPublish,
        EnvironmentCheck,
        GlobBookkeeping,
        StatClassification,
        Count,
    }

    private readonly LockType _lock = new();
    private readonly byte[] _hashKey = new byte[32];
    private readonly string _traceId = Guid.NewGuid().ToString("N");
    private readonly int _processId = EnvironmentUtilities.CurrentProcessId;
    private readonly Dictionary<string, string> _history = new(StringComparer.Ordinal);
    private readonly Queue<string> _historyOrder = new();
    private readonly List<string> _events = [];
    private readonly SortedDictionary<string, long> _counts = new(StringComparer.Ordinal);
    private readonly PhaseTiming[] _timings = new PhaseTiming[(int)Phase.Count];
    private readonly TimedBuckets?[] _phaseBuckets = new TimedBuckets?[(int)Phase.Count];
    private readonly SortedDictionary<string, TimedBuckets> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SlowPath> _slowPaths = new(StringComparer.Ordinal);
    private long _slowPathsDropped;
    private ProcessSnapshot? _processStart;
    private readonly List<string> _examples = [];
    private readonly Dictionary<string, int> _exampleCounts = new(StringComparer.Ordinal);
    private long _suppressedExamples;
    private string _buildManagerId = string.Empty;
    private long _buildNumber;
    private long _nextRequestId;
    private long _requestCount;
    private int _droppedEvents;
    private int _forgottenHistory;
    private EvaluationCacheMode _mode;

    internal EvaluationCacheDiagnostics()
    {
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        random.GetBytes(_hashKey);
    }

    internal IReadOnlyDictionary<string, long> Environment { get; private set; } =
        new Dictionary<string, long>(StringComparer.Ordinal);

    internal void BeginBuild(
        string buildManagerId,
        long buildNumber,
        EvaluationCacheMode mode,
        IEnumerable<ProjectPropertyInstance> environment)
    {
        var snapshot = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (ProjectPropertyInstance property in environment)
        {
            snapshot[property.Name] = FowlerNollVo1aHash.ComputeHash64Fast(
                ((IProperty)property).EvaluatedValueEscaped);
        }
        ProcessSnapshot processStart = ProcessSnapshot.Capture(includeWorkingSet: false);
        lock (_lock)
        {
            _buildManagerId = buildManagerId;
            _buildNumber = buildNumber;
            _mode = mode;
            Environment = snapshot;
            _processStart = processStart;
            RecordCore(null, "Lifecycle", "BuildStarted", null);
        }
    }

    internal Request StartRequest(string project, int configurationId, int submissionId, int nodeId)
    {
        lock (_lock)
        {
            _requestCount++;
            return new Request(this, ++_nextRequestId, project, configurationId, submissionId, nodeId);
        }
    }

    internal string GetKeyId(ProjectInstanceSnapshotCacheKey key)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            key.WriteDiagnosticIdentity(writer);
        }
        stream.Position = 0;
        using var hash = new HMACSHA256(_hashKey);
        return Convert.ToBase64String(hash.ComputeHash(stream));
    }

    internal string? GetPreviousDisposition(string keyId)
    {
        lock (_lock)
        {
            return _history.TryGetValue(keyId, out string? reason) ? reason : null;
        }
    }

    internal void Remember(string keyId, string reason)
    {
        lock (_lock)
        {
            if (!_history.ContainsKey(keyId))
            {
                if (_history.Count == MaximumHistoryEntries)
                {
                    _history.Remove(_historyOrder.Dequeue());
                    _forgottenHistory++;
                }
                _historyOrder.Enqueue(keyId);
            }
            _history[keyId] = reason;
        }
    }

    internal void RecordRemoval(ProjectInstanceSnapshotCacheKey key, string reason)
    {
        string keyId = GetKeyId(key);
        Remember(keyId, reason);
        Record(new Request(this, 0, key.ProjectFullPath, -1, -1, -1) { KeyId = keyId },
            "Lifecycle", reason);
    }

    internal void RecordKeyMismatch(
        Request request,
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheKey candidate,
        IReadOnlyDictionary<string, long>? candidateEnvironment,
        List<string> differences)
    {
        List<string> changedGlobals = key.GetChangedGlobalPropertyNames(candidate);
        List<string> changedEnvironment = [];
        if (candidateEnvironment is not null)
        {
            foreach (KeyValuePair<string, long> current in Environment)
            {
                if (!candidateEnvironment.TryGetValue(current.Key, out long previous) || previous != current.Value)
                {
                    changedEnvironment.Add(current.Key);
                }
            }
            foreach (string name in candidateEnvironment.Keys)
            {
                if (!Environment.ContainsKey(name))
                {
                    changedEnvironment.Add(name);
                }
            }
            changedEnvironment.Sort(StringComparer.Ordinal);
        }

        string detail = $"CandidateKey={GetKeyId(candidate)};Fields={JoinNames(differences)};GlobalPropertyNames={JoinNames(changedGlobals)};EnvironmentVariableNames={(candidateEnvironment is null ? "<unavailable>" : JoinNames(changedEnvironment))}";
        lock (_lock)
        {
            foreach (string difference in differences)
            {
                Increment("CandidateDifference." + difference);
            }
            RecordCore(request, "Lookup", "KeyMismatch", detail);
        }
    }

    internal void Record(Request? request, string action, string reason, string? detail = null)
    {
        lock (_lock)
        {
            RecordCore(request, action, reason, detail);
        }
    }

    internal void RecordTiming(Phase phase, string project, long elapsedTicks)
    {
        lock (_lock)
        {
            RecordTimingCore(phase, project, elapsedTicks);
        }
    }

    /// <summary>Marks a request entering a phase and returns the timestamp it started at.</summary>
    internal long EnterPhase(Phase phase)
    {
        lock (_lock)
        {
            ref PhaseTiming timing = ref _timings[(int)phase];
            long now = Stopwatch.GetTimestamp();
            if (timing.Active++ == 0)
            {
                timing.BusyStartTicks = now;
            }

            if (timing.Active > timing.PeakActive)
            {
                timing.PeakActive = timing.Active;
            }

            return now;
        }
    }

    internal void ExitPhase(Phase phase, string project, long startTicks, long endTicks)
    {
        lock (_lock)
        {
            RecordTimingCore(phase, project, endTicks - startTicks);
            ref PhaseTiming timing = ref _timings[(int)phase];

            // A flush between enter and exit resets the phase, so only a phase that is still active can go idle.
            if (timing.Active > 0 && --timing.Active == 0)
            {
                timing.BusyTicks += endTicks - timing.BusyStartTicks;
            }
        }
    }

    internal void AddBuckets(string family, TimedBuckets buckets)
    {
        lock (_lock)
        {
            if (!_buckets.TryGetValue(family, out TimedBuckets? total))
            {
                _buckets[family] = total = new TimedBuckets(buckets.Labels);
            }

            total.Merge(buckets);
        }
    }

    internal void AddSlowStats(List<(string Path, long Ticks)> stats)
    {
        lock (_lock)
        {
            foreach ((string path, long ticks) in stats)
            {
                if (_slowPaths.TryGetValue(path, out SlowPath existing))
                {
                    _slowPaths[path] = new SlowPath(existing.Count + 1, existing.Ticks + ticks);
                }
                else if (_slowPaths.Count < MaximumSlowPaths)
                {
                    _slowPaths[path] = new SlowPath(1, ticks);
                }
                else
                {
                    _slowPathsDropped++;
                }
            }
        }
    }

    private readonly record struct SlowPath(long Count, long Ticks);

    private void RecordTimingCore(Phase phase, string project, long elapsedTicks)
    {
        ref PhaseTiming timing = ref _timings[(int)phase];
        timing.Count++;
        timing.TotalTicks += elapsedTicks;
        if (timing.SlowestProject is null || elapsedTicks > timing.MaxTicks)
        {
            timing.MaxTicks = elapsedTicks;
            timing.SlowestProject = project;
        }

        (_phaseBuckets[(int)phase] ??= new TimedBuckets(LatencyBuckets.Labels)).Add(LatencyBuckets.Index(elapsedTicks), elapsedTicks);
    }

    internal void AddCount(string name, long delta)
    {
        lock (_lock)
        {
            _counts[name] = _counts.TryGetValue(name, out long count) ? count + delta : delta;
        }
    }

    internal void Flush(ILoggingService loggingService, BuildEventContext? context = null)
    {
        string[] events;
        string[] examples;
        string summary;
        string timingSummary;
        string identity;
        PhaseTiming[] timings;
        TimedBuckets?[] phaseBuckets;
        KeyValuePair<string, TimedBuckets>[] bucketFamilies;
        KeyValuePair<string, SlowPath>[] slowPaths;
        long slowPathsDropped;
        ProcessSnapshot? processStart;
        lock (_lock)
        {
            events = [.. _events];
            var counts = new StringBuilder();
            foreach (KeyValuePair<string, long> count in _counts)
            {
                if (counts.Length > 0)
                {
                    counts.Append(',');
                }
                counts.Append(count.Key).Append(':').Append(count.Value);
            }
            summary = FormattableString.Invariant($"EvaluationCacheDiagnosticSummary|Version=1|{Context()}|Requests={_requestCount}|DroppedEvents={_droppedEvents}|ForgottenHistory={_forgottenHistory}|Counts={counts}");
            identity = Context();
            timingSummary = FormattableString.Invariant($"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=Counts|Requests={_requestCount}|DroppedEvents={_droppedEvents}|ForgottenHistory={_forgottenHistory}|SuppressedExamples={_suppressedExamples}|StopwatchFrequency={Stopwatch.Frequency}|Counts={counts}");
            timings = (PhaseTiming[])_timings.Clone();
            phaseBuckets = (TimedBuckets?[])_phaseBuckets.Clone();
            bucketFamilies = [.. _buckets];
            slowPaths = [.. _slowPaths];
            slowPathsDropped = _slowPathsDropped;
            processStart = _processStart;
            examples = [.. _examples];
            _events.Clear();
            _counts.Clear();
            Array.Clear(_timings, 0, _timings.Length);
            Array.Clear(_phaseBuckets, 0, _phaseBuckets.Length);
            _buckets.Clear();
            _slowPaths.Clear();
            _slowPathsDropped = 0;
            _processStart = null;
            _examples.Clear();
            _exampleCounts.Clear();
            _suppressedExamples = 0;
            _requestCount = 0;
            _droppedEvents = 0;
            _forgottenHistory = 0;
        }

        // The end of the measured window is taken before this flush's own logging, which would otherwise be charged to the build.
        ProcessSnapshot? processEnd = processStart is null ? null : ProcessSnapshot.Capture(includeWorkingSet: true);

        foreach (string message in events)
        {
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.Low, message);
        }
        loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.Low, summary);
        loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, timingSummary);
        for (int i = 0; i < timings.Length; i++)
        {
            PhaseTiming timing = timings[i];
            if (timing.Count == 0)
            {
                continue;
            }

            Phase phase = (Phase)i;
            string nestedUnder = phase switch
            {
                Phase.ManifestValidation or Phase.SdkValidation or Phase.RootElementCheck or Phase.KeyCheck or Phase.DiagnosticsPublish
                    => nameof(Phase.Validation),
                Phase.FileStatLoop or Phase.GlobReplay or Phase.EnvironmentCheck or Phase.GlobBookkeeping or Phase.StatClassification
                    => nameof(Phase.ManifestValidation),
                _ => string.Empty,
            };

            // Only phases entered as scopes know how many requests were inside them; the rest are added after the fact.
            string concurrency = timing.PeakActive == 0
                ? string.Empty
                : FormattableString.Invariant($"|PeakActive={timing.PeakActive}|BusyMilliseconds={timing.BusyTicks * 1000.0 / Stopwatch.Frequency:F3}");
            string message = FormattableString.Invariant(
                $"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=Phase|Phase={phase}|NestedUnder={nestedUnder}|Count={timing.Count}|TotalTicks={timing.TotalTicks}|MaxTicks={timing.MaxTicks}|TotalMilliseconds={timing.TotalTicks * 1000.0 / Stopwatch.Frequency:F3}|MaxMilliseconds={timing.MaxTicks * 1000.0 / Stopwatch.Frequency:F3}|SlowestProject={EscapeBounded(timing.SlowestProject)}{concurrency}");
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, message);
        }
        for (int i = 0; i < phaseBuckets.Length; i++)
        {
            if (phaseBuckets[i] is { } durations)
            {
                LogBuckets(loggingService, context, identity, "Phase." + (Phase)i, durations);
            }
        }
        foreach (KeyValuePair<string, TimedBuckets> family in bucketFamilies)
        {
            LogBuckets(loggingService, context, identity, family.Key, family.Value);
        }
        LogSlowPaths(loggingService, context, identity, slowPaths, slowPathsDropped);
        if (processStart is { } started && processEnd is { } ended)
        {
            string process = FormattableString.Invariant(
                $"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=Process|WallMilliseconds={(ended.Timestamp - started.Timestamp) * 1000.0 / Stopwatch.Frequency:F3}|CpuMilliseconds={ended.CpuMilliseconds - started.CpuMilliseconds:F3}|GcCollections0={ended.Collections0 - started.Collections0}|GcCollections1={ended.Collections1 - started.Collections1}|GcCollections2={ended.Collections2 - started.Collections2}|GcPauseMilliseconds={Delta(started.GcPauseMilliseconds, ended.GcPauseMilliseconds):F3}|AllocatedBytes={Delta(started.AllocatedBytes, ended.AllocatedBytes)}|WorkingSetBytes={ended.WorkingSetBytes}|ProcessorCount={System.Environment.ProcessorCount}");
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, process);
        }
        foreach (string message in examples)
        {
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, message);
        }
    }

    // A counter the runtime does not provide is -1 in both snapshots, and its difference stays -1.
    private static double Delta(double start, double end) => start < 0 || end < 0 ? -1 : end - start;

    private static long Delta(long start, long end) => start < 0 || end < 0 ? -1 : end - start;

    private static void LogSlowPaths(
        ILoggingService loggingService,
        BuildEventContext? context,
        string identity,
        KeyValuePair<string, SlowPath>[] slowPaths,
        long dropped)
    {
        if (slowPaths.Length == 0)
        {
            return;
        }

        BuildEventContext target = context ?? BuildEventContext.Invalid;
        long totalCount = 0;
        long totalTicks = 0;
        var directories = new Dictionary<string, (long Count, long Ticks, int Paths)>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, SlowPath> entry in slowPaths)
        {
            totalCount += entry.Value.Count;
            totalTicks += entry.Value.Ticks;
            string directory = Path.GetDirectoryName(entry.Key) ?? string.Empty;
            (long count, long ticks, int paths) = directories.TryGetValue(directory, out var existing) ? existing : default;
            directories[directory] = (count + entry.Value.Count, ticks + entry.Value.Ticks, paths + 1);
        }

        loggingService.LogCommentFromText(
            target,
            MessageImportance.High,
            FormattableString.Invariant($"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=SlowPathTotals|DistinctPaths={slowPaths.Length}|DistinctDirectories={directories.Count}|DroppedStats={dropped}|Count={totalCount}|Microseconds={Microseconds(totalTicks)}"));

        Array.Sort(slowPaths, static (left, right) => right.Value.Ticks.CompareTo(left.Value.Ticks));
        for (int i = 0; i < Math.Min(ReportedSlowPaths, slowPaths.Length); i++)
        {
            loggingService.LogCommentFromText(
                target,
                MessageImportance.High,
                FormattableString.Invariant($"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=SlowPath|Rank={i + 1}|Count={slowPaths[i].Value.Count}|Microseconds={Microseconds(slowPaths[i].Value.Ticks)}|Path={EscapeBounded(slowPaths[i].Key)}"));
        }

        KeyValuePair<string, (long Count, long Ticks, int Paths)>[] byDirectory = [.. directories];
        Array.Sort(byDirectory, static (left, right) => right.Value.Ticks.CompareTo(left.Value.Ticks));
        for (int i = 0; i < Math.Min(ReportedSlowDirectories, byDirectory.Length); i++)
        {
            loggingService.LogCommentFromText(
                target,
                MessageImportance.High,
                FormattableString.Invariant($"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=SlowDirectory|Rank={i + 1}|Count={byDirectory[i].Value.Count}|DistinctPaths={byDirectory[i].Value.Paths}|Microseconds={Microseconds(byDirectory[i].Value.Ticks)}|Path={EscapeBounded(byDirectory[i].Key)}"));
        }
    }

    private static long Microseconds(long ticks) => ticks * 1_000_000 / Stopwatch.Frequency;

    private static void LogBuckets(ILoggingService loggingService, BuildEventContext? context, string identity, string family, TimedBuckets buckets)
    {
        string used = buckets.Format();
        if (used.Length > 0)
        {
            loggingService.LogCommentFromText(
                context ?? BuildEventContext.Invalid,
                MessageImportance.High,
                FormattableString.Invariant($"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=Buckets|Family={family}|Buckets={used}"));
        }
    }

    private void RecordCore(Request? request, string action, string reason, string? detail)
    {
        Increment(action + "." + reason);
        RecordExample(request, action, reason, detail);
        if (_events.Count == MaximumEventsPerBuild)
        {
            _droppedEvents++;
            return;
        }
        _events.Add(FormattableString.Invariant(
            $"EvaluationCacheDiagnostic|Version=1|{Context()}|Request={request?.Id ?? 0}|Configuration={request?.ConfigurationId ?? -1}|Submission={request?.SubmissionId ?? -1}|Node={request?.NodeId ?? -1}|Project={Escape(request?.Project)}|Key={Escape(request?.KeyId)}|Event={action}|Reason={reason}|Detail={Escape(detail)}"));
    }

    private void RecordExample(Request? request, string action, string reason, string? detail)
    {
        bool meaningful = action switch
        {
            "Validation" => reason is not ("Accepted" or "UnsafeBypass"),
            "Admission" => reason is not "Stored",
            "Lookup" => reason is not ("CandidateFound" or "NoEntryOrHistory"),
            "Bypass" => reason == "CacheUnavailableOnHost",
            "Fallback" => true,
            _ => false,
        };
        if (!meaningful)
        {
            return;
        }

        string category = action + "." + reason;
        _exampleCounts.TryGetValue(category, out int count);
        if (count == MaximumExamplesPerReason || _examples.Count == MaximumExamplesPerBuild)
        {
            _suppressedExamples++;
            return;
        }

        _exampleCounts[category] = count + 1;
        _examples.Add(FormattableString.Invariant(
            $"EvaluationCacheTimingExample|Version=1|{Context()}|Request={request?.Id ?? 0}|Configuration={request?.ConfigurationId ?? -1}|Submission={request?.SubmissionId ?? -1}|Node={request?.NodeId ?? -1}|Project={EscapeBounded(request?.Project)}|Key={Escape(request?.KeyId)}|Event={action}|Reason={reason}|Detail={EscapeBounded(detail)}"));
    }

    private static string EscapeBounded(string? value)
    {
        if (value is { Length: > MaximumExampleFieldLength })
        {
            int length = MaximumExampleFieldLength;
            if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            {
                length--;
            }
            value = value.Substring(0, length) + "<truncated>";
        }

        return Escape(value);
    }

    private struct PhaseTiming
    {
        internal long Count;
        internal long TotalTicks;
        internal long MaxTicks;
        internal string? SlowestProject;

        // Requests inside the phase right now, the most at once, and the time at least one request was inside it.
        internal int Active;
        internal int PeakActive;
        internal long BusyStartTicks;
        internal long BusyTicks;
    }

    /// <summary>What one build cost the whole process, so thread time can be compared with wall time, GC and memory.</summary>
    private readonly struct ProcessSnapshot
    {
        private ProcessSnapshot(
            long timestamp,
            int collections0,
            int collections1,
            int collections2,
            double gcPauseMilliseconds,
            long allocatedBytes,
            double cpuMilliseconds,
            long workingSetBytes)
        {
            Timestamp = timestamp;
            Collections0 = collections0;
            Collections1 = collections1;
            Collections2 = collections2;
            GcPauseMilliseconds = gcPauseMilliseconds;
            AllocatedBytes = allocatedBytes;
            CpuMilliseconds = cpuMilliseconds;
            WorkingSetBytes = workingSetBytes;
        }

        internal long Timestamp { get; }

        internal int Collections0 { get; }

        internal int Collections1 { get; }

        internal int Collections2 { get; }

        internal double GcPauseMilliseconds { get; }

        internal long AllocatedBytes { get; }

        internal double CpuMilliseconds { get; }

        internal long WorkingSetBytes { get; }

        internal static ProcessSnapshot Capture(bool includeWorkingSet)
        {
            using Process process = Process.GetCurrentProcess();
            long timestamp = Stopwatch.GetTimestamp();
            double cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds;
#if NET
            double gcPauseMilliseconds = GC.GetTotalPauseDuration().TotalMilliseconds;
            long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
#else
            double gcPauseMilliseconds = -1;
            long allocatedBytes = -1;
#endif

            // Reading the working set can enumerate processes, so it is only taken where it is reported, and last.
            long workingSetBytes = includeWorkingSet ? process.WorkingSet64 : 0;
            return new ProcessSnapshot(
                timestamp,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2),
                gcPauseMilliseconds,
                allocatedBytes,
                cpuMilliseconds,
                workingSetBytes);
        }
    }

    // Only constructed through a non-null diagnostic request. Disposal only updates in-memory counters and
    // histograms under the owner's lock. It does no formatting, logging or cache work, including when unwinding an exception.
    internal readonly struct TimingScope(EvaluationCacheDiagnostics owner, Phase phase, string project) : IDisposable
    {
        private readonly long _start = owner.EnterPhase(phase);

        public void Dispose() => owner.ExitPhase(phase, project, _start, Stopwatch.GetTimestamp());
    }

    private string Context() => FormattableString.Invariant(
        $"ProcessId={_processId}|BuildManagerId={Escape(_buildManagerId)}|TraceId={_traceId}|Build={_buildNumber}|Mode={_mode}");

    private void Increment(string name) =>
        _counts[name] = _counts.TryGetValue(name, out long count) ? count + 1 : 1;

    private static string JoinNames(List<string> names) =>
        string.Join(",", names.ConvertAll(static name => Escape(name).Replace(",", "%2C").Replace(";", "%3B")));

    internal static string Escape(string? value) =>
        value?.Replace("%", "%25").Replace("|", "%7C").Replace("=", "%3D")
            .Replace("\r", "%0D").Replace("\n", "%0A") ?? string.Empty;

    internal sealed class Request(
        EvaluationCacheDiagnostics owner,
        long id,
        string project,
        int configurationId,
        int submissionId,
        int nodeId)
    {
        internal long Id { get; } = id;
        internal string Project { get; } = project;
        internal int ConfigurationId { get; } = configurationId;
        internal int SubmissionId { get; } = submissionId;
        internal int NodeId { get; } = nodeId;
        internal string? KeyId { get; set; }

        internal TimingScope Time(Phase phase) => new(owner, phase, Project);

        internal void AddTiming(Phase phase, long elapsedTicks) => owner.RecordTiming(phase, Project, elapsedTicks);

        internal void AddCount(string name, long delta) => owner.AddCount(name, delta);

        internal void AddBuckets(string family, TimedBuckets buckets) => owner.AddBuckets(family, buckets);

        internal void AddSlowStats(List<(string Path, long Ticks)> stats) => owner.AddSlowStats(stats);

        internal void BindKey(ProjectInstanceSnapshotCacheKey key) => KeyId = owner.GetKeyId(key);

        internal void Record(string action, string reason, string? detail = null) =>
            owner.Record(this, action, reason, detail);

        internal void Remember(string reason)
        {
            if (KeyId is not null)
            {
                owner.Remember(KeyId, reason);
            }
        }
    }
}
