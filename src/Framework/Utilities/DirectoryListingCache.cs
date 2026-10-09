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


    internal void Store(string directory, FileMatcher.FileSystemEntity kind, IReadOnlyList<string> entries, DateTime lastWriteTimeUtc)
    {
        IReadOnlyList<string> snapshot = ImmutableArray.CreateRange(entries);
        _listings[(directory, kind)] = new Listing(snapshot, lastWriteTimeUtc);
    }

    internal bool TryGet(string directory, FileMatcher.FileSystemEntity kind, [NotNullWhen(true)] out IReadOnlyList<string>? entries)
    {
        if (_listings.TryGetValue((directory, kind), out Listing listing)
            && IsUpToDate(directory, listing))
        {
            entries = listing.Entries;
            return true;
        }

        entries = null;
        return false;
    }

    private bool IsUpToDate(string directory, Listing listing)
    {
        if (TryReadDirectoryTimestamp(directory, out DateTime current)
            && listing.LastWriteTimeUtc == current)
        {
            return true;
        }

        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Files), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.Directories), out _);
        _listings.TryRemove((directory, FileMatcher.FileSystemEntity.FilesAndDirectories), out _);
        return false;
    }

    internal static bool TryReadDirectoryTimestamp(string directory, out DateTime lastWriteTimeUtc)
    {
        try
        {
            return NativeMethods.GetLastWriteDirectoryUtcTime(directory, out lastWriteTimeUtc);
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            DebugTrace.WriteLine($"Could not read directory timestamp for '{directory}': {ex.Message}", category: nameof(DirectoryListingCache));
            lastWriteTimeUtc = default;
            return false;
        }
    }

    internal void Clear() => _listings.Clear();

    // Files in folder and timestamp of last modified of this folder.
    private readonly record struct Listing(IReadOnlyList<string> Entries, DateTime LastWriteTimeUtc);
}
