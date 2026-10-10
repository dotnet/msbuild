// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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
        _shared = SharedStates.GetValue(cache, _ => new SharedState());
    }

    internal static GlobResultExperiment? Create(DirectoryListingCache cache)
        => Mode == "validated" ? new GlobResultExperiment(cache) : null;

    internal static long[] GetStatistics()
        => [Volatile.Read(ref _lookups), Volatile.Read(ref _hits), Volatile.Read(ref _invalidated),
            Volatile.Read(ref _stores), Volatile.Read(ref _probes), Volatile.Read(ref _probeTicks),
            Volatile.Read(ref _dependencies), Volatile.Read(ref _resultEntries), Stopwatch.Frequency];

    internal static void Clear(DirectoryListingCache cache)
    {
        if (SharedStates.TryGetValue(cache, out SharedState? state)) { state.Results.Clear(); }
    }

    internal void RecordListing(Collector? collector, string path, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries)
    {
        if (collector is null) { return; }
        if (!_listings.IsSnapshot(path, kind, entries)) { collector.Reject(); return; }
        collector.Record((path, (int)kind), new Dependency(path, (int)kind, entries, true));
    }

    internal bool TryGet(string key, FileMatcher.GetFileSystemEntries rawEntries, Func<string, bool> directoryExists, out string[] files)
    {
        Interlocked.Increment(ref _lookups);
        if (_shared.Results.TryGetValue(key, out Entry? entry))
        {
            if (Validate(entry.Dependencies, rawEntries, directoryExists))
            {
                Interlocked.Increment(ref _hits);
                files = entry.Files;
                return true;
            }
            Interlocked.Increment(ref _invalidated);
            ((ICollection<KeyValuePair<string, Entry>>)_shared.Results).Remove(new KeyValuePair<string, Entry>(key, entry));
        }
        files = [];
        return false;
    }

    private static bool Validate(ImmutableArray<Dependency> dependencies, FileMatcher.GetFileSystemEntries rawEntries, Func<string, bool> directoryExists)
    {
        foreach (Dependency dependency in dependencies)
        {
            long started = Stopwatch.GetTimestamp();
            bool valid = dependency.Kind < 0
                ? directoryExists(dependency.Path) == dependency.Exists
                : ReferenceEquals(rawEntries((FileMatcher.FileSystemEntity)dependency.Kind, dependency.Path, "*", null, false), dependency.Entries);
            Interlocked.Increment(ref _probes);
            Interlocked.Add(ref _probeTicks, Stopwatch.GetTimestamp()-started);
            if (!valid) { return false; }
        }
        return true;
    }

    internal void Store(string key, string[] files, FileMatcher.SearchAction action, string? failure, Collector collector)
    {
        if (failure is not null || action is not (FileMatcher.SearchAction.None
            or FileMatcher.SearchAction.RunSearch or FileMatcher.SearchAction.ReturnEmptyList)
            || !collector.Admissible)
        {
            return;
        }
        var dependencies = ImmutableArray.CreateBuilder<Dependency>(collector.Dependencies.Count);
        foreach (Dependency dependency in collector.Dependencies.Values) { dependencies.Add(dependency); }
        if (dependencies.Count == 0) { return; }
        if (_shared.Results.TryAdd(key, new Entry(files, dependencies.MoveToImmutable())))
        {
            Interlocked.Increment(ref _stores);
            Interlocked.Add(ref _dependencies, collector.Dependencies.Count);
            Interlocked.Add(ref _resultEntries, files.Length);
        }
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
    private sealed class Entry(string[] files, ImmutableArray<Dependency> dependencies)
    {
        internal readonly string[] Files = files;
        internal readonly ImmutableArray<Dependency> Dependencies = dependencies;
    }

    private sealed class SharedState
    {
        internal readonly ConcurrentDictionary<string, Entry> Results = new(StringComparer.Ordinal);
    }
}
