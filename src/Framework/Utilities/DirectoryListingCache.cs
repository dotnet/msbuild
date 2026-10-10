// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared;

/// <summary>
/// Caches directory listings for reuse across project evaluations and builds.
/// </summary>
internal sealed class DirectoryListingCache
{
    private readonly ConcurrentDictionary<(string Directory, FileMatcher.FileSystemEntity Kind), Listing> _listings = new();
    private readonly ValidationMode _validationMode = ReadValidationMode();
    internal int DiagnosticId { get; } = CachePerformanceDiagnostics.NextCacheId();
    internal string DiagnosticValidationMode => _validationMode.ToString();
    internal int DiagnosticListingCount => _listings.Count;

    internal IReadOnlyList<string> Store(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries, DirectoryStamp stamp)
    {
        using var measurement = CachePerformanceDiagnostics.Measure(CachePerformanceDiagnostics.Operation.ListingStore);
        var key = (directory, kind);
        IReadOnlyList<string>? snapshot = null;
        while (true)
        {
            bool exists = _listings.TryGetValue(key, out Listing current);
            if (exists && current.Stamp == stamp)
            {
                return current.Entries;
            }

            snapshot ??= entries is ImmutableArray<string> ? entries : ImmutableArray.CreateRange(entries);
            var replacement = new Listing(snapshot, stamp);
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
        CachePerformanceDiagnostics.UseCache(this);
        using var measurement = CachePerformanceDiagnostics.Measure(CachePerformanceDiagnostics.Operation.SharedListingLookup);
        if (_listings.TryGetValue((directory, kind), out Listing listing)
            && IsUpToDate(directory, listing))
        {
            entries = listing.Entries;
            CachePerformanceDiagnostics.Add(CachePerformanceDiagnostics.Operation.SharedListingHit);
            return true;
        }

        entries = null;
        CachePerformanceDiagnostics.Add(CachePerformanceDiagnostics.Operation.SharedListingMiss);
        return false;
    }

    internal bool IsSnapshot(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries)
        => _listings.TryGetValue((directory, kind), out Listing listing) && ReferenceEquals(listing.Entries, entries);

    private bool IsUpToDate(string directory, Listing listing)
    {
        if (TryReadDirectoryStamp(directory, out DirectoryStamp current)
            && listing.Stamp == current)
        {
            return true;
        }

        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Files), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Directories), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.FilesAndDirectories), out _);
        CachePerformanceDiagnostics.Add(CachePerformanceDiagnostics.Operation.SharedListingInvalidated);
        return false;
    }

    internal static bool TryReadDirectoryTimestamp(string directory, out DateTime lastWriteTimeUtc)
    {
        try
        {
            return NativeMethods.GetLastWriteDirectoryUtcTime(directory, out lastWriteTimeUtc, rejectReparsePoints: true);
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            DebugTrace.WriteLine($"Could not read directory timestamp for '{directory}': {ex.Message}", category: nameof(DirectoryListingCache));
            lastWriteTimeUtc = default;
            return false;
        }
    }

    internal bool TryReadDirectoryStamp(string directory, out DirectoryStamp stamp)
    {
        CachePerformanceDiagnostics.UseCache(this);
        using var measurement = CachePerformanceDiagnostics.Measure(CachePerformanceDiagnostics.Operation.Metadata);
        bool succeeded = TryReadStamp(directory, out stamp);
        if (!succeeded) { CachePerformanceDiagnostics.Add(CachePerformanceDiagnostics.Operation.MetadataUnavailable); }
        return succeeded;
    }

    private bool TryReadStamp(string directory, out DirectoryStamp stamp)
    {
        if (_validationMode == ValidationMode.Strong)
        {
            return DirectoryMetadata.TryRead(directory, out stamp);
        }
        if (_validationMode == ValidationMode.Timestamp && TryReadDirectoryTimestamp(directory, out DateTime timestamp))
        {
            stamp = new DirectoryStamp(0, 0, 0, timestamp.Ticks, 0);
            return true;
        }
        stamp = default;
        return false;
    }

    private static ValidationMode ReadValidationMode()
    {
        string? mode = Environment.GetEnvironmentVariable("MSBUILDDIRECTORYCACHEVALIDATION");
        if (string.IsNullOrEmpty(mode) || string.Equals(mode, "strong", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationMode.Strong;
        }
        if (string.Equals(mode, "timestamp", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationMode.Timestamp;
        }
        DebugTrace.WriteLine($"Unknown MSBUILDDIRECTORYCACHEVALIDATION value '{mode}'; shared directory reuse is disabled.", category: nameof(DirectoryListingCache));
        return ValidationMode.Disabled;
    }

    internal void Clear()
    {
        _listings.Clear();
        GlobResultExperiment.Clear(this);
    }

    private enum ValidationMode
    {
        Strong,
        Timestamp,
        Disabled,
    }

    private readonly record struct Listing(IReadOnlyList<string> Entries, DirectoryStamp Stamp);
}
