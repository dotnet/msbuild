// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Stores project instance snapshots by evaluation identity.
/// </summary>
internal sealed class ProjectInstanceSnapshotCache : IBuildComponent
{
    internal const long DefaultMaximumSizeBytes = 1024L * 1024 * 1024;
    internal const string MaximumSizeEnvironmentVariable =
        "MSBUILDPROJECTINSTANCESNAPSHOTCACHEMAXBYTES";

    private readonly LockType _lock = new();
    private readonly Dictionary<ProjectInstanceSnapshotCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = [];
    private readonly ImmutableFileStatCache _immutableFileStats = new();
    private readonly ValidatedDirectoryListings _validatedDirectoryListings = new();
    private readonly long _maximumSizeBytes;
    private long _currentSizeBytes;
    private long _buildsServed;
    private long _cacheHits;
    private long _cacheMisses;
    private long _freshEvaluations;
    private long _recordedEvaluations;
    private long _nonCacheableEvaluations;
    private long _validationAttempts;
    private long _validationAccepted;
    private long _validationRejections;
    private long _validationErrors;
    private long _materializedEntries;
    private long _storedEntries;
    private long _evictedEntries;
    private long _oversizedRejections;
    private long _fallbacks;
    private IProjectInstanceSnapshotValidator _validator =
        RejectingProjectInstanceSnapshotValidator.Instance;

    internal ProjectInstanceSnapshotCache(long maximumSizeBytes = DefaultMaximumSizeBytes)
    {
        if (maximumSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSizeBytes));
        }

        _maximumSizeBytes = maximumSizeBytes;
    }

    internal int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    internal long CurrentSizeBytes
    {
        get
        {
            lock (_lock)
            {
                return _currentSizeBytes;
            }
        }
    }

    internal long MaximumSizeBytes => _maximumSizeBytes;

    internal EvaluationCacheDiagnostics? Diagnostics { get; private set; }

    internal void ConfigureDiagnostics(
        bool enabled,
        string buildManagerId,
        long buildNumber,
        EvaluationCacheMode mode,
        IEnumerable<ProjectPropertyInstance> environment)
    {
        lock (_lock)
        {
            if (!enabled)
            {
                if (Diagnostics is not null)
                {
                    Diagnostics = null;
                    foreach (CacheEntry entry in _leastRecentlyUsed)
                    {
                        entry.DiagnosticEnvironment = null;
                    }
                }
                return;
            }

            Diagnostics ??= new EvaluationCacheDiagnostics();
            Diagnostics.BeginBuild(buildManagerId, buildNumber, mode, environment);
        }
    }

    internal long BuildsServed
    {
        get
        {
            lock (_lock)
            {
                return _buildsServed;
            }
        }
    }

    internal long StoredEntries
    {
        get
        {
            lock (_lock)
            {
                return _storedEntries;
            }
        }
    }

    internal long CacheHits
    {
        get
        {
            lock (_lock)
            {
                return _cacheHits;
            }
        }
    }

    internal long CacheMisses
    {
        get
        {
            lock (_lock)
            {
                return _cacheMisses;
            }
        }
    }

    internal long ValidationRejections
    {
        get
        {
            lock (_lock)
            {
                return _validationRejections;
            }
        }
    }

    internal long MaterializedEntries
    {
        get
        {
            lock (_lock)
            {
                return _materializedEntries;
            }
        }
    }

    internal ProjectInstanceSnapshotCacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new ProjectInstanceSnapshotCacheStatistics(
                _buildsServed,
                _entries.Count,
                _currentSizeBytes,
                _maximumSizeBytes,
                _storedEntries,
                _cacheHits,
                _cacheMisses,
                _freshEvaluations,
                _recordedEvaluations,
                _nonCacheableEvaluations,
                _validationAttempts,
                _validationAccepted,
                _validationRejections,
                _validationErrors,
                _materializedEntries,
                _evictedEntries,
                _oversizedRejections,
                _fallbacks);
        }
    }

    internal IProjectInstanceSnapshotValidator Validator
    {
        get
        {
            lock (_lock)
            {
                return _validator;
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_lock)
            {
                _validator = value;
            }
        }
    }

    /// <summary>
    /// Metadata of SDK and package files that no build step modifies, shared by every entry validated in the
    /// current build. Cleared when a build starts so a server or other long-lived host sees later changes.
    /// </summary>
    internal ImmutableFileStatCache ImmutableFileStats => _immutableFileStats;

    /// <summary>
    /// Directory listings read by glob replay, reused only while each directory still has the state its listing was read at.
    /// They outlive a build because every use compares that state; they are discarded with the cache's entries.
    /// </summary>
    internal ValidatedDirectoryListings ValidatedDirectoryListings => _validatedDirectoryListings;

    internal void NotifyBuildStarted()
    {
        lock (_lock)
        {
            _buildsServed++;
        }

        _immutableFileStats.NotifyBuildStarted();
    }

    internal void NotifyCacheLookup(bool hit)
    {
        lock (_lock)
        {
            if (hit)
            {
                _cacheHits++;
            }
            else
            {
                _cacheMisses++;
            }
        }
    }

    internal void NotifyFreshEvaluation(Evaluation.Context.EvaluationInputs? inputs)
    {
        lock (_lock)
        {
            _freshEvaluations++;
            if (inputs is not null)
            {
                _recordedEvaluations++;
                if (!inputs.IsCacheable)
                {
                    _nonCacheableEvaluations++;
                }
            }
        }
    }

    internal void NotifyValidationAccepted()
    {
        lock (_lock)
        {
            _validationAttempts++;
            _validationAccepted++;
        }
    }

    internal void NotifyValidationRejected()
    {
        lock (_lock)
        {
            _validationAttempts++;
            _validationRejections++;
        }
    }

    internal void NotifyValidationError()
    {
        lock (_lock)
        {
            _validationAttempts++;
            _validationErrors++;
        }
    }

    internal void NotifyFallback()
    {
        lock (_lock)
        {
            _fallbacks++;
        }
    }

    internal void NotifyMaterialized()
    {
        lock (_lock)
        {
            _materializedEntries++;
        }
    }

    internal bool TryGet(
        ProjectInstanceSnapshotCacheKey key,
        out ProjectInstanceSnapshotCacheEntry? entry,
        EvaluationCacheDiagnostics.Request? diagnosticRequest = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                entry = null;
                if (diagnosticRequest is not null && Diagnostics is not null)
                {
                    ExplainMiss(key, diagnosticRequest, Diagnostics);
                }
                return false;
            }

            MarkMostRecentlyUsed(node);
            entry = node.Value.Entry;
            diagnosticRequest?.Record("Lookup", "CandidateFound");
            return true;
        }
    }

    internal bool AddOrReplace(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry,
        EvaluationCacheDiagnostics.Request? diagnosticRequest = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            var cacheEntry = new CacheEntry(key, entry)
            {
                DiagnosticEnvironment = Diagnostics?.Environment,
            };
            if (cacheEntry.SizeBytes > _maximumSizeBytes)
            {
                RemoveCore(key);
                _oversizedRejections++;
                diagnosticRequest?.Record("Admission", "Oversized");
                if (Diagnostics is not null)
                {
                    Diagnostics.Remember(Diagnostics.GetKeyId(key), "Oversized");
                }
                return false;
            }
            RemoveCore(key);

            while (_leastRecentlyUsed.Count > 0
                && _currentSizeBytes > _maximumSizeBytes - cacheEntry.SizeBytes)
            {
                EvictLeastRecentlyUsed();
            }

            var node = new LinkedListNode<CacheEntry>(cacheEntry);
            _entries.Add(key, node);
            _leastRecentlyUsed.AddFirst(node);
            _currentSizeBytes += cacheEntry.SizeBytes;

            _storedEntries++;
            if (Diagnostics is not null)
            {
                Diagnostics.Remember(Diagnostics.GetKeyId(key), "Stored");
            }
            diagnosticRequest?.Record("Admission", "Stored");
            return true;
        }
    }

    internal bool Remove(ProjectInstanceSnapshotCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            return RemoveCore(key);
        }
    }

    internal bool Remove(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry expectedEntry)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(expectedEntry);
        lock (_lock)
        {
            return _entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node)
                && ReferenceEquals(node.Value.Entry, expectedEntry)
                && RemoveCore(key);
        }
    }

    internal void ConfigureValidator(EvaluationCacheValidationPolicy policy)
    {
        Validator = policy switch
        {
            EvaluationCacheValidationPolicy.Unsafe => UnsafeProjectInstanceSnapshotValidator.Instance,
            EvaluationCacheValidationPolicy.FileSystem => FileSystemProjectInstanceSnapshotValidator.Instance,
            _ => RejectingProjectInstanceSnapshotValidator.Instance,
        };
    }

    internal void Clear(string reason = "CacheCleared")
    {
        lock (_lock)
        {
            if (Diagnostics is not null)
            {
                foreach (CacheEntry entry in _leastRecentlyUsed)
                {
                    Diagnostics.RecordRemoval(entry.Key, reason);
                }
            }
            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _currentSizeBytes = 0;
        }

        _validatedDirectoryListings.Clear();
    }

    /// <summary>
    /// Initializes the cache for its owning component host.
    /// </summary>
    public void InitializeComponent(IBuildComponentHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
    }

    /// <summary>
    /// Releases all snapshots owned by this component.
    /// </summary>
    public void ShutdownComponent() => Clear("ComponentShutdown");

    /// <summary>
    /// Creates the singleton component instance for a build-component host.
    /// </summary>
    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        Assumed.Equal(
            type,
            BuildComponentType.ProjectInstanceSnapshotCache,
            $"Cannot create components of type {type}");
        long maximumSizeBytes = DefaultMaximumSizeBytes;
        string? configuredMaximum =
            Environment.GetEnvironmentVariable(MaximumSizeEnvironmentVariable);
        if (!string.IsNullOrEmpty(configuredMaximum) &&
            long.TryParse(
                configuredMaximum,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long parsedMaximum) &&
            parsedMaximum >= 0)
        {
            maximumSizeBytes = parsedMaximum;
        }

        return new ProjectInstanceSnapshotCache(maximumSizeBytes);
    }

    // Caller must hold _lock.
    private bool RemoveCore(ProjectInstanceSnapshotCacheKey key)
    {
        if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
        {
            return false;
        }

        _entries.Remove(key);
        _leastRecentlyUsed.Remove(node);
        _currentSizeBytes -= node.Value.SizeBytes;
        Diagnostics?.RecordRemoval(node.Value.Key, "EntryRemoved");
        return true;
    }

    // Caller must hold _lock.
    private void MarkMostRecentlyUsed(LinkedListNode<CacheEntry> node)
    {
        _leastRecentlyUsed.Remove(node);
        _leastRecentlyUsed.AddFirst(node);
    }

    // Caller must hold _lock.
    private void EvictLeastRecentlyUsed()
    {
        LinkedListNode<CacheEntry> node = _leastRecentlyUsed.Last!;
        _leastRecentlyUsed.RemoveLast();
        _entries.Remove(node.Value.Key);
        _currentSizeBytes -= node.Value.SizeBytes;
        _evictedEntries++;
        Diagnostics?.RecordRemoval(node.Value.Key, "Evicted");
    }

    // Cache lock precedes the diagnostics lock. Diagnostics only buffer events here;
    // invoking the logging service is deferred until EndBuild, outside both locks.
    private void ExplainMiss(
        ProjectInstanceSnapshotCacheKey key,
        EvaluationCacheDiagnostics.Request request,
        EvaluationCacheDiagnostics diagnostics)
    {
        string? previous = request.KeyId is null ? null : diagnostics.GetPreviousDisposition(request.KeyId);
        if (previous is not null && previous != "Stored")
        {
            request.Record("Lookup", previous);
            return;
        }

        CacheEntry? candidate = null;
        List<string>? differences = null;
        foreach (CacheEntry current in _leastRecentlyUsed)
        {
            if (!FileUtilities.PathComparer.Equals(key.ProjectFullPath, current.Key.ProjectFullPath))
            {
                continue;
            }
            List<string> currentDifferences = key.GetDiagnosticDifferences(current.Key);
            if (differences is null || currentDifferences.Count < differences.Count)
            {
                candidate = current;
                differences = currentDifferences;
            }
        }

        if (candidate is not null)
        {
            diagnostics.RecordKeyMismatch(request, key, candidate.Key, candidate.DiagnosticEnvironment, differences!);
        }
        else
        {
            request.Record("Lookup", "NoEntryOrHistory");
        }
    }

    private sealed class CacheEntry
    {
        internal CacheEntry(
            ProjectInstanceSnapshotCacheKey key,
            ProjectInstanceSnapshotCacheEntry entry)
        {
            Key = key;
            Entry = entry;
        }

        internal ProjectInstanceSnapshotCacheKey Key { get; }

        internal ProjectInstanceSnapshotCacheEntry Entry { get; }

        internal IReadOnlyDictionary<string, long>? DiagnosticEnvironment { get; set; }

        internal long SizeBytes =>
            Microsoft.Build.Evaluation.Context.RetainedSizeEstimator.Add(
                Key.RetainedSizeBytes,
                Entry.RetainedSizeBytes);
    }
}

internal readonly record struct ProjectInstanceSnapshotCacheStatistics(
    long BuildsServed,
    int Count,
    long CurrentSizeBytes,
    long MaximumSizeBytes,
    long StoredEntries,
    long CacheHits,
    long CacheMisses,
    long FreshEvaluations,
    long RecordedEvaluations,
    long NonCacheableEvaluations,
    long ValidationAttempts,
    long ValidationAccepted,
    long ValidationRejections,
    long ValidationErrors,
    long MaterializedEntries,
    long EvictedEntries,
    long OversizedRejections,
    long Fallbacks);
