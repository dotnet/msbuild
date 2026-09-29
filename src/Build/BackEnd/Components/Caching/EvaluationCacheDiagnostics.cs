// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
/// Bounded, opt-in decision tracing. This state never participates in cache acceptance or admission.
/// </summary>
internal sealed class EvaluationCacheDiagnostics
{
    internal const string EnvironmentVariable = Traits.EvaluationCacheDiagnosticsEnvVarName;
    internal const int MaximumHistoryEntries = 2048;
    internal const int MaximumEventsPerBuild = 10000;

    private readonly LockType _lock = new();
    private readonly byte[] _hashKey = new byte[32];
    private readonly string _traceId = Guid.NewGuid().ToString("N");
    private readonly int _processId = EnvironmentUtilities.CurrentProcessId;
    private readonly Dictionary<string, string> _history = new(StringComparer.Ordinal);
    private readonly Queue<string> _historyOrder = new();
    private readonly List<string> _events = [];
    private readonly SortedDictionary<string, long> _counts = new(StringComparer.Ordinal);
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

    internal void Flush(ILoggingService loggingService, BuildEventContext? context = null)
    {
        string[] events;
        string summary;
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
            _events.Clear();
            _counts.Clear();
            _requestCount = 0;
            _droppedEvents = 0;
            _forgottenHistory = 0;
        }

        foreach (string message in events)
        {
            loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.Low, message);
        }
        loggingService.LogCommentFromText(context ?? BuildEventContext.Invalid, MessageImportance.Low, summary);
    }

    private void RecordCore(Request? request, string action, string reason, string? detail)
    {
        Increment(action + "." + reason);
        if (_events.Count == MaximumEventsPerBuild)
        {
            _droppedEvents++;
            return;
        }
        _events.Add(FormattableString.Invariant(
            $"EvaluationCacheDiagnostic|Version=1|{Context()}|Request={request?.Id ?? 0}|Configuration={request?.ConfigurationId ?? -1}|Submission={request?.SubmissionId ?? -1}|Node={request?.NodeId ?? -1}|Project={Escape(request?.Project)}|Key={Escape(request?.KeyId)}|Event={action}|Reason={reason}|Detail={Escape(detail)}"));
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
