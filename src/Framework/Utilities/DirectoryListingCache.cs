// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
#if FEATURE_WINDOWSINTEROP
using System.Threading;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared;

/// <summary>
/// Caches directory listings for reuse across project evaluations and builds.
/// </summary>
internal sealed class DirectoryListingCache
{
    private const int MaximumUnsupportedDirectories = 65536;
    private const int MaximumMissingPaths = 65536;
    private readonly ConcurrentDictionary<(string Directory, FileMatcher.FileSystemEntity Kind), Listing> _listings;
    private readonly string[] _excludedRoots;
    private readonly DirectoryStampReader _readStamp;
    private readonly ConcurrentDictionary<string, byte> _unsupportedDirectories;
    private readonly ConcurrentDictionary<string, MissingPath> _missingPaths;
    private readonly DirectoryListingCache? _owner;
    private readonly Dictionary<string, StampRead>? _evaluationStamps;
#if FEATURE_WINDOWSINTEROP
    private readonly DirectoryJournal? _journal;
    private readonly bool _nativeJournal;
    private long _journalEvaluationVersion = -1;
#endif

    internal interface ISubscription : IDisposable
    {
        DirectorySnapshotStatus Validate(DirectoryListingCache cache);
    }

    internal DirectoryListingCache() : this((DirectoryStampReader?)null)
    {
    }

    internal DirectoryListingCache(DirectoryStampReader? readStamp)
    {
        _listings = new();
        _unsupportedDirectories = new(FileUtilities.PathComparer);
        _missingPaths = new(StringComparer.Ordinal);
        string? excludedRoots = Environment.GetEnvironmentVariable("MSBUILDDIRECTORYCACHEEXCLUDEROOTS");
        _excludedRoots = string.IsNullOrEmpty(excludedRoots)
            ? []
            : excludedRoots.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries);
        try
        {
            for (int i = 0; i < _excludedRoots.Length; i++)
            {
                _excludedRoots[i] = Path.GetFullPath(_excludedRoots[i].Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            _excludedRoots = [""];
        }

#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows)
        {
            _journal = new DirectoryJournal();
            _nativeJournal = readStamp is null;
        }
#endif
        _readStamp = readStamp ?? ReadNativeStamp;
    }

    private DirectoryListingCache(DirectoryListingCache owner)
    {
        _owner = owner;
        _listings = owner._listings;
        _excludedRoots = owner._excludedRoots;
        _readStamp = owner._readStamp;
        _unsupportedDirectories = owner._unsupportedDirectories;
        _missingPaths = owner._missingPaths;
        _evaluationStamps = new(StringComparer.Ordinal);
#if FEATURE_WINDOWSINTEROP
        _journal = owner._journal;
        _nativeJournal = owner._nativeJournal;
#endif
    }

    internal DirectoryListingCache Shared => _owner ?? this;

    internal DirectoryListingCache CreateEvaluationCache() => new(Shared);

    internal DirectoryListingCache ForEvaluation() => _owner is not null ? this : CreateEvaluationCache();

    internal bool TrySubscribe(ImmutableArray<GlobResultExperiment.Dependency> dependencies, out ISubscription? subscription)
    {
        subscription = null;
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows && _nativeJournal && _journal is not null)
        {
            Dictionary<string, DirectoryChangeStamp> stamps = new(StringComparer.OrdinalIgnoreCase);
            foreach (GlobResultExperiment.Dependency dependency in dependencies)
            {
                string path = dependency.Path;
                DirectoryChangeStamp stamp;
                if (dependency.Kind >= 0)
                {
                    if (!_listings.TryGetValue((path, (FileMatcher.FileSystemEntity)dependency.Kind), out Listing listing)
                        || !ReferenceEquals(listing.Entries, dependency.Entries)) { return false; }
                    stamp = listing.ChangeTime;
                }
                else if (dependency.Exists)
                {
                    if (!TryReadDirectoryChangeTime(path, out stamp)) { return false; }
                }
                else
                {
                    if (!TryNormalizeProbePath(path, out string fullPath)
                        || !_missingPaths.TryGetValue(fullPath, out MissingPath missing)) { return false; }
                    path = missing.Parent;
                    stamp = missing.Stamp;
                }
                if (stamps.TryGetValue(path, out DirectoryChangeStamp previous) && previous != stamp) { return false; }
                stamps[path] = stamp;
            }
            subscription = _journal.Subscribe(stamps);
            return subscription is not null;
        }
#endif
        return true;
    }

#if FEATURE_WINDOWSINTEROP
    internal bool ObserveJournalVersion(long version)
    {
        if (_evaluationStamps is null) { return true; }
        long previous = Interlocked.CompareExchange(ref _journalEvaluationVersion, version, -1);
        return previous == -1 || previous == version;
    }
#endif

    internal bool CanStore(DirectoryChangeStamp stamp, DateTime utcNow)
        => NativeMethods.IsWindows || stamp.IsOldEnoughToCache(utcNow);

    internal IReadOnlyList<string> Store(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries, DirectoryChangeStamp changeTime)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.ListingStore, directory);
        var key = (directory, kind);
        IReadOnlyList<string>? snapshot = null;
        while (true)
        {
            bool exists = _listings.TryGetValue(key, out Listing current);
            if (exists && current.ChangeTime == changeTime)
            {
                return current.Entries;
            }

            snapshot ??= entries is ImmutableArray<string> ? entries : ImmutableArray.CreateRange(entries);
            var replacement = new Listing(snapshot, changeTime);
            if (exists
                ? _listings.TryUpdate(key, replacement, current)
                : _listings.TryAdd(key, replacement))
            {
                return snapshot;
            }
        }
    }

    internal bool TryGet(string directory, FileMatcher.FileSystemEntity kind, [NotNullWhen(true)] out IReadOnlyList<string>? entries)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.ListingLookup, directory);
        if (_listings.TryGetValue((directory, kind), out Listing listing)
            && IsUpToDate(directory, listing))
        {
            entries = listing.Entries;
            CacheAttribution.Count(CacheAttribution.Op.ListingHit);
            return true;
        }

        entries = null;
        CacheAttribution.Count(CacheAttribution.Op.ListingMiss);
        return false;
    }

    internal bool IsSnapshot(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries)
        => _listings.TryGetValue((directory, kind), out Listing listing) && ReferenceEquals(listing.Entries, entries);

    internal DirectorySnapshotStatus TryValidate(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries)
    {
        if (!_listings.TryGetValue((directory, kind), out Listing listing) || !ReferenceEquals(listing.Entries, entries))
        {
            return DirectorySnapshotStatus.Mismatch;
        }

        return Validate(directory, listing);
    }

    private bool IsUpToDate(string directory, Listing listing)
        => Validate(directory, listing) == DirectorySnapshotStatus.Valid;

    private DirectorySnapshotStatus Validate(string directory, Listing listing)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.ListingValidate, directory);
        DirectoryStampStatus status = ReadStamp(directory, out DirectoryChangeStamp current);
        if (status == DirectoryStampStatus.Unavailable)
        {
            return DirectorySnapshotStatus.Unavailable;
        }

        if (status == DirectoryStampStatus.Available && listing.ChangeTime == current)
        {
            return DirectorySnapshotStatus.Valid;
        }

        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Files), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Directories), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.FilesAndDirectories), out _);
        return DirectorySnapshotStatus.Mismatch;
    }

    internal bool TryReadDirectoryChangeTime(string directory, out DirectoryChangeStamp changeTime)
        => ReadStamp(directory, out changeTime) == DirectoryStampStatus.Available;

    internal bool TryReadDirectoryChangeTimeAfterEnumeration(string directory, out DirectoryChangeStamp changeTime)
        => ReadUncachedStamp(directory, out changeTime) == DirectoryStampStatus.Available;

    internal DirectorySnapshotStatus ValidateMissingPath(string path)
    {
        if (_missingPaths.IsEmpty || !TryNormalizeProbePath(path, out string fullPath)
            || !_missingPaths.TryGetValue(fullPath, out MissingPath missing))
        {
            return DirectorySnapshotStatus.Mismatch;
        }

        DirectoryStampStatus status = ReadStamp(missing.Parent, out DirectoryChangeStamp stamp);
        if (status == DirectoryStampStatus.Unavailable) { return DirectorySnapshotStatus.Unavailable; }
        if (status == DirectoryStampStatus.Available && stamp == missing.Stamp)
        {
            return DirectorySnapshotStatus.Valid;
        }

        ((ICollection<KeyValuePair<string, MissingPath>>)_missingPaths).Remove(new(fullPath, missing));
        return DirectorySnapshotStatus.Mismatch;
    }

    internal bool TryRecordMissingPath(string path, IFileSystem fileSystem)
    {
        if (!TryNormalizeProbePath(path, out string fullPath)) { return false; }
        try
        {
            string missingPath = fullPath;
            string? parent = Path.GetDirectoryName(missingPath);
            while (parent is not null)
            {
                DirectoryStampStatus status = ReadStamp(parent, out DirectoryChangeStamp stamp);
                if (status == DirectoryStampStatus.Available)
                {
                    if (!CanStore(stamp, DateTime.UtcNow)) { return false; }
                    try
                    {
                        fileSystem.GetAttributes(missingPath);
                        return false;
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }

                    if (_missingPaths.Count >= MaximumMissingPaths && !_missingPaths.ContainsKey(fullPath)) { return false; }
                    _missingPaths[fullPath] = new(parent, stamp);
                    return true;
                }
                if (status != DirectoryStampStatus.Missing) { return false; }
                missingPath = parent;
                parent = Path.GetDirectoryName(parent);
            }
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            DebugTrace.WriteLine($"Could not qualify missing path '{path}': {ex.Message}", category: nameof(DirectoryListingCache));
        }
        return false;
    }

    private bool TryNormalizeProbePath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            fullPath = Path.GetFullPath(path);
            // Short names and alternate streams are not ordinary directory-membership dependencies.
            return !(NativeMethods.IsWindows && (fullPath.IndexOf('~') >= 0 || fullPath.IndexOf(':', 3) >= 0))
                && CanAttemptSharing(fullPath);
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            return false;
        }
    }

    internal bool CanAttemptSharing(string directory)
    {
        if (_unsupportedDirectories.ContainsKey(directory) || IsExcluded(directory))
        {
            return false;
        }
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows && _journal is not null) { return _journal.CanAttemptSharing(directory); }
#endif
        return true;
    }

    private DirectoryStampStatus ReadStamp(string directory, out DirectoryChangeStamp changeTime)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.Stamp, directory);
        if (_evaluationStamps is null) { return ReadUncachedStamp(directory, out changeTime); }
        using (CacheAttribution.Lock(_evaluationStamps, CacheAttribution.Op.MemoWait, CacheAttribution.Op.MemoHold))
        {
            if (!_evaluationStamps.TryGetValue(directory, out StampRead read))
            {
                CacheAttribution.Count(CacheAttribution.Op.StampMemoMiss);
                DirectoryStampStatus status = ReadUncachedStamp(directory, out DirectoryChangeStamp stamp);
                read = new(status, stamp);
                _evaluationStamps.Add(directory, read);
            }
            else { CacheAttribution.Count(CacheAttribution.Op.StampMemoHit); }
            changeTime = read.Stamp;
            return read.Status;
        }
    }

    private DirectoryStampStatus ReadUncachedStamp(string directory, out DirectoryChangeStamp changeTime)
    {
        using var attribution = CacheAttribution.Measure(CacheAttribution.Op.NativeStamp, directory);
        try
        {
            if (!CanAttemptSharing(directory))
            {
                changeTime = default;
                return DirectoryStampStatus.Unavailable;
            }

            DirectoryStampStatus status = _readStamp(directory, out changeTime);
#if FEATURE_WINDOWSINTEROP
            if (NativeMethods.IsWindows && _nativeJournal && _journal is not null)
            {
                ObserveJournalVersion(_journal.ChangeVersion);
            }
#endif
            return status;
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            DebugTrace.WriteLine($"Could not read directory change time for '{directory}': {ex.Message}", category: nameof(DirectoryListingCache));
            changeTime = default;
            return DirectoryStampStatus.Unavailable;
        }
    }

    private DirectoryStampStatus ReadNativeStamp(string directory, out DirectoryChangeStamp stamp)
    {
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows && _journal is not null) { return _journal.Read(directory, out stamp); }
#endif
        DirectoryStampStatus status = DirectoryChangeTime.Read(directory, out stamp, out bool unsupportedFileSystem);
        if (unsupportedFileSystem && _unsupportedDirectories.Count < MaximumUnsupportedDirectories)
        {
            _unsupportedDirectories.TryAdd(directory, 0);
        }
        return status;
    }

    internal bool IsExcluded(string directory)
    {
        if (_excludedRoots.Length == 0)
        {
            return false;
        }

        string fullPath = Path.GetFullPath(directory);
        foreach (string root in _excludedRoots)
        {
            if (fullPath.StartsWith(root, FileUtilities.PathComparison)
                && (root.Length == 0 || fullPath.Length == root.Length || FileUtilities.IsAnySlash(fullPath[root.Length])))
            {
                return true;
            }
        }

        return false;
    }

    internal void Clear()
    {
        if (_owner is not null)
        {
            lock (_evaluationStamps!) { _evaluationStamps.Clear(); }
#if FEATURE_WINDOWSINTEROP
            Interlocked.Exchange(ref _journalEvaluationVersion, -1);
#endif
            _owner.Clear();
            return;
        }
        _listings.Clear();
        _unsupportedDirectories.Clear();
        _missingPaths.Clear();
        GlobResultExperiment.Clear(this);
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows) { _journal?.Clear(); }
#endif
    }

    private readonly record struct Listing(IReadOnlyList<string> Entries, DirectoryChangeStamp ChangeTime);
    private readonly record struct StampRead(DirectoryStampStatus Status, DirectoryChangeStamp Stamp);
    private readonly record struct MissingPath(string Parent, DirectoryChangeStamp Stamp);
}
