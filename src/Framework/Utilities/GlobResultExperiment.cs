// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared;

internal sealed class GlobResultExperiment
{
    private static string? Mode = Environment.GetEnvironmentVariable("MSBUILDGLOBRESULTEXPERIMENT") ?? "validated";
    private static readonly ConditionalWeakTable<DirectoryListingCache, SharedState> SharedStates = new();
    private static long _lookups;
    private static long _hits;
    private static long _invalidated;
    private static long _stores;
    private static long _probes;
    private static long _probeTicks;
    private static long _dependencies;
    private static long _resultEntries;
    private readonly SharedState _shared;
    private readonly DirectoryListingCache _listings;

    private GlobResultExperiment(DirectoryListingCache cache)
    {
        _listings = cache;
        _shared = SharedStates.GetValue(cache.Shared, _ => new SharedState());
    }

    internal static GlobResultExperiment? Create(DirectoryListingCache cache)
        => Mode == "validated" ? new GlobResultExperiment(cache) : null;

    internal static long[] GetStatistics()
        => [Volatile.Read(ref _lookups), Volatile.Read(ref _hits), Volatile.Read(ref _invalidated),
            Volatile.Read(ref _stores), Volatile.Read(ref _probes), Volatile.Read(ref _probeTicks),
            Volatile.Read(ref _dependencies), Volatile.Read(ref _resultEntries), Stopwatch.Frequency];

    internal static void Clear(DirectoryListingCache cache)
    {
        if (SharedStates.TryGetValue(cache.Shared, out SharedState? state))
        {
            lock (state.Gate)
            {
                foreach (Entry entry in state.Results.Values) { entry.Subscription?.Dispose(); }
                state.Results.Clear();
            }
        }
    }

    internal void RecordListing(Collector? collector, string path, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries)
    {
        if (collector is null) { return; }
        if (!_listings.IsSnapshot(path, kind, entries)) { collector.Reject(); return; }
        collector.Record((path, (int)kind), new Dependency(path, (int)kind, entries, true));
    }

    internal void RecordExistence(Collector? collector, string path, bool exists, IFileSystem? fileSystem = null)
    {
        if (collector is null)
        {
            return;
        }

        if (!_listings.CanAttemptSharing(path)
            || (!exists && (fileSystem is null || !FileSystems.SupportsDirectoryCache(fileSystem)
                || (_listings.ValidateMissingPath(path) != DirectorySnapshotStatus.Valid
                    && !_listings.TryRecordMissingPath(path, fileSystem)))))
        {
            collector.Reject();
            return;
        }

        collector.RecordExistence(path, exists);
    }

    internal delegate DirectorySnapshotStatus SnapshotValidator(string path, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries);

    internal bool CanAttemptSharing(string directory) => _listings.CanAttemptSharing(directory);

    internal bool TryGet(string key, SnapshotValidator validateSnapshot, Func<string, bool> directoryExists, out string[] files)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.GlobLookup);
        Interlocked.Increment(ref _lookups);
        if (_shared.Results.TryGetValue(key, out Entry? entry))
        {
            DirectorySnapshotStatus status;
            if (entry.Subscription is { } subscription)
            {
                using var validation = CacheAttribution.Measure(CacheAttribution.Op.GlobValidate);
                validation.Units(1);
                long started = Stopwatch.GetTimestamp();
                status = subscription.Validate(_listings);
                Interlocked.Increment(ref _probes);
                Interlocked.Add(ref _probeTicks, Stopwatch.GetTimestamp() - started);
            }
            else
            {
                status = Validate(entry.Dependencies, validateSnapshot, directoryExists);
            }
            if (status == DirectorySnapshotStatus.Valid)
            {
                Interlocked.Increment(ref _hits);
                files = entry.Files;
                return true;
            }
            if (status == DirectorySnapshotStatus.Mismatch)
            {
                Interlocked.Increment(ref _invalidated);
                lock (_shared.Gate)
                {
                    if (((ICollection<KeyValuePair<string, Entry>>)_shared.Results).Remove(new(key, entry)))
                    {
                        entry.Subscription?.Dispose();
                    }
                }
            }
        }
        files = [];
        return false;
    }

    private DirectorySnapshotStatus Validate(ImmutableArray<Dependency> dependencies, SnapshotValidator validateSnapshot, Func<string, bool> directoryExists)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.GlobValidate);
        attribution.Units(dependencies.Length);
        DirectorySnapshotStatus result = DirectorySnapshotStatus.Valid;
        foreach (Dependency dependency in dependencies)
        {
            long started = Stopwatch.GetTimestamp();
            DirectorySnapshotStatus status;
            try
            {
                status = dependency.Kind switch
                {
                    < 0 when dependency.Exists => directoryExists(dependency.Path)
                        ? DirectorySnapshotStatus.Valid : DirectorySnapshotStatus.Mismatch,
                    < 0 => _listings.ValidateMissingPath(dependency.Path),
                    _ => validateSnapshot(dependency.Path, (FileMatcher.FileSystemEntity)dependency.Kind, dependency.Entries!),
                };
            }
            catch (Exception ex) when (FileMatcher.IsIoRelatedExceptionOrAggregate(ex))
            {
                DebugTrace.WriteLine($"Could not validate cached glob dependency '{dependency.Path}': {ex.Message}", category: nameof(GlobResultExperiment));
                status = dependency.Kind >= 0
                    && _listings.IsSnapshot(dependency.Path, (FileMatcher.FileSystemEntity)dependency.Kind, dependency.Entries!)
                    ? DirectorySnapshotStatus.Unavailable : DirectorySnapshotStatus.Mismatch;
            }
            Interlocked.Increment(ref _probes);
            Interlocked.Add(ref _probeTicks, Stopwatch.GetTimestamp() - started);
            if (status == DirectorySnapshotStatus.Mismatch) { return status; }
            if (status == DirectorySnapshotStatus.Unavailable) { result = status; }
        }
        return result;
    }

    internal void Store(string key, string[] files, FileMatcher.SearchAction action, string? failure, Collector collector)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.GlobStore);
        if (failure is not null || action is not (FileMatcher.SearchAction.None
            or FileMatcher.SearchAction.RunSearch or FileMatcher.SearchAction.ReturnEmptyList)
            || !collector.Admissible)
        {
            return;
        }
        var dependencies = ImmutableArray.CreateBuilder<Dependency>(collector.Dependencies.Count);
        foreach (Dependency dependency in collector.Dependencies.Values)
        {
            if (dependency.Kind < 0
                && (dependency.Exists
                    ? (!collector.Dependencies.ContainsKey((dependency.Path, (int)FileMatcher.FileSystemEntity.Files))
                        && !collector.Dependencies.ContainsKey((dependency.Path, (int)FileMatcher.FileSystemEntity.Directories))
                        && !collector.Dependencies.ContainsKey((dependency.Path, (int)FileMatcher.FileSystemEntity.FilesAndDirectories))
                        && !_listings.TryReadDirectoryChangeTime(dependency.Path, out _))
                    : _listings.ValidateMissingPath(dependency.Path) != DirectorySnapshotStatus.Valid))
            {
                return;
            }
            dependencies.Add(dependency);
        }
        if (dependencies.Count == 0) { return; }
        ImmutableArray<Dependency> captured = dependencies.MoveToImmutable();
        DirectoryListingCache.ISubscription? subscription = null;
        try
        {
            if (!_listings.TrySubscribe(captured, out subscription)) { return; }
            lock (_shared.Gate)
            {
                // Result-cache locks may enter the journal; journal invalidation never calls the result cache.
                if (_shared.Results.TryAdd(key, new Entry(files, captured, subscription)))
                {
                    subscription = null;
                    Interlocked.Increment(ref _stores);
                    Interlocked.Add(ref _dependencies, collector.Dependencies.Count);
                    Interlocked.Add(ref _resultEntries, files.Length);
                }
            }
        }
        finally { subscription?.Dispose(); }
    }

    internal sealed class Collector
    {
        private int _rejected;
        internal readonly ConcurrentDictionary<(string Path, int Kind), Dependency> Dependencies = new();
        internal bool Admissible => Volatile.Read(ref _rejected) == 0;
        internal void Reject() => Interlocked.Exchange(ref _rejected, 1);
        internal void Record((string Path, int Kind) key, Dependency dependency)
        {
            Dependency first = Dependencies.GetOrAdd(key, dependency);
            if (first != dependency) { Reject(); }
        }

        internal void RecordExistence(string path, bool exists)
            => Record((path, -1), new Dependency(path, -1, null, exists));
    }

    internal readonly record struct Dependency(string Path, int Kind, IReadOnlyList<string>? Entries, bool Exists);
    private sealed class Entry(string[] files, ImmutableArray<Dependency> dependencies, DirectoryListingCache.ISubscription? subscription)
    {
        internal readonly string[] Files = files;
        internal readonly ImmutableArray<Dependency> Dependencies = dependencies;
        internal readonly DirectoryListingCache.ISubscription? Subscription = subscription;
    }

    private sealed class SharedState
    {
        internal readonly object Gate = new();
        internal readonly ConcurrentDictionary<string, Entry> Results = new(StringComparer.Ordinal);
    }
}
