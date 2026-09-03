// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Stores project instance snapshots by evaluation identity.
/// </summary>
internal sealed class ProjectInstanceSnapshotCache : IBuildComponent
{
    internal const long DefaultMaximumSizeBytes = 256L * 1024 * 1024;
    internal const string MaximumSizeEnvironmentVariable =
        "MSBUILDPROJECTINSTANCESNAPSHOTCACHEMAXBYTES";

    private readonly LockType _lock = new();
    private readonly Dictionary<ProjectInstanceSnapshotCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = [];
    private readonly long _maximumSizeBytes;
    private long _currentSizeBytes;
    private long _buildsServed;
    private long _cacheHits;
    private long _cacheMisses;
    private long _validationRejections;
    private long _materializedEntries;
    private long _storedEntries;
    private long _evictedEntries;
    private long _oversizedRejections;
    private long _snapshotCreationTicks;
    private long _materializationTicks;
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
                _validationRejections,
                _materializedEntries,
                _evictedEntries,
                _oversizedRejections,
                _snapshotCreationTicks,
                _materializationTicks);
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

    internal void NotifyBuildStarted()
    {
        lock (_lock)
        {
            _buildsServed++;
        }
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

    internal void NotifyValidationRejected()
    {
        lock (_lock)
        {
            _validationRejections++;
        }
    }

    internal void NotifyMaterialized(long elapsedTicks)
    {
        lock (_lock)
        {
            _materializedEntries++;
            _materializationTicks += elapsedTicks;
        }
    }

    internal void NotifySnapshotCreated(long elapsedTicks)
    {
        lock (_lock)
        {
            _snapshotCreationTicks += elapsedTicks;
        }
    }

    internal bool TryGet(
        ProjectInstanceSnapshotCacheKey key,
        out ProjectInstanceSnapshotCacheEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                entry = null;
                return false;
            }

            MarkMostRecentlyUsed(node);
            entry = node.Value.Entry;
            return true;
        }
    }

    internal bool AddOrReplace(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            if (entry.RetainedSizeBytes > _maximumSizeBytes)
            {
                RemoveCore(key);
                _oversizedRejections++;
                return false;
            }

            RemoveCore(key);

            var cacheEntry = new CacheEntry(key, entry);
            var node = new LinkedListNode<CacheEntry>(cacheEntry);
            _entries.Add(key, node);
            _leastRecentlyUsed.AddFirst(node);
            _currentSizeBytes += cacheEntry.SizeBytes;

            EvictToSizeLimit();
            _storedEntries++;
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

    internal void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _currentSizeBytes = 0;
        }
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
    public void ShutdownComponent() => Clear();

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
        return true;
    }

    // Caller must hold _lock.
    private void MarkMostRecentlyUsed(LinkedListNode<CacheEntry> node)
    {
        _leastRecentlyUsed.Remove(node);
        _leastRecentlyUsed.AddFirst(node);
    }

    // Caller must hold _lock.
    private void EvictToSizeLimit()
    {
        while (_currentSizeBytes > _maximumSizeBytes)
        {
            LinkedListNode<CacheEntry> node = _leastRecentlyUsed.Last!;
            _leastRecentlyUsed.RemoveLast();
            _entries.Remove(node.Value.Key);
            _currentSizeBytes -= node.Value.SizeBytes;
            _evictedEntries++;
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

        internal long SizeBytes => Entry.RetainedSizeBytes;
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
    long ValidationRejections,
    long MaterializedEntries,
    long EvictedEntries,
    long OversizedRejections,
    long SnapshotCreationTicks,
    long MaterializationTicks);
