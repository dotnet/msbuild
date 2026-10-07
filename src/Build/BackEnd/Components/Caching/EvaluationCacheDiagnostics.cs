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
        lock (_lock)
        {
            _buildManagerId = buildManagerId;
            _buildNumber = buildNumber;
            _mode = mode;
            Environment = snapshot;
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
            ref PhaseTiming timing = ref _timings[(int)phase];
            timing.Count++;
            timing.TotalTicks += elapsedTicks;
            if (timing.SlowestProject is null || elapsedTicks > timing.MaxTicks)
            {
                timing.MaxTicks = elapsedTicks;
                timing.SlowestProject = project;
            }
        }
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
            examples = [.. _examples];
            _events.Clear();
            _counts.Clear();
            Array.Clear(_timings, 0, _timings.Length);
            _examples.Clear();
            _exampleCounts.Clear();
            _suppressedExamples = 0;
            _requestCount = 0;
            _droppedEvents = 0;
            _forgottenHistory = 0;
        }

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
                Phase.ManifestValidation or Phase.SdkValidation => nameof(Phase.Validation),
                Phase.FileStatLoop or Phase.GlobReplay => nameof(Phase.ManifestValidation),
                _ => string.Empty,
            };
            string message = FormattableString.Invariant(
                $"EvaluationCacheTimingSummary|Version=1|{identity}|Kind=Phase|Phase={phase}|NestedUnder={nestedUnder}|Count={timing.Count}|TotalTicks={timing.TotalTicks}|MaxTicks={timing.MaxTicks}|TotalMilliseconds={timing.TotalTicks * 1000.0 / Stopwatch.Frequency:F3}|MaxMilliseconds={timing.MaxTicks * 1000.0 / Stopwatch.Frequency:F3}|SlowestProject={EscapeBounded(timing.SlowestProject)}");
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, message);
        }
        foreach (string message in examples)
        {
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.High, message);
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
    }

    // Only constructed through a non-null diagnostic request. Disposal does no formatting,
    // allocation, logging or cache work, including when unwinding an exception.
    internal readonly struct TimingScope(EvaluationCacheDiagnostics owner, Phase phase, string project) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();

        public void Dispose() => owner.RecordTiming(phase, project, Stopwatch.GetTimestamp() - _start);
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
