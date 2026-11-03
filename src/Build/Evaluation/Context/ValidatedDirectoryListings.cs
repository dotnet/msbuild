// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Directory listings that glob replay read from the file system, kept with the state of the directory observed before
/// they were read. A later replay takes a listing from here only while the directory still has exactly that state, so it
/// reads from the file system just the directories that changed. A state is only trusted when its timestamp is old enough
/// that a later change cannot share it, and listings are only kept from a replay in which every glob matched.
/// </summary>
internal sealed class ValidatedDirectoryListings
{
    internal const int DefaultMaximumDirectories = 100_000;

    /// <summary>
    /// A directory timestamp this close to now cannot prove the directory is quiet: a file system with coarse timestamps
    /// (FAT stamps in two-second steps) could give a later change the same timestamp.
    /// </summary>
    internal static readonly TimeSpan RacyTimestampWindow = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, Listing> _directories = new(FileUtilities.PathComparer);
    private readonly int _maximumDirectories;

    internal ValidatedDirectoryListings(int maximumDirectories = DefaultMaximumDirectories)
    {
        _maximumDirectories = maximumDirectories;
    }

    internal int DirectoryCount => _directories.Count;

    internal void Clear() => _directories.Clear();

    /// <summary>
    /// Starts one entry's replay, supplying the listings of every directory whose current state is the state its
    /// stored listings were read at.
    /// </summary>
    /// <param name="directoryStates">The state each glob-traversed directory has now, observed before the replay reads anything.</param>
    internal Replay BeginReplay(IReadOnlyList<KeyValuePair<string, FileDependency>> directoryStates)
    {
        var listings = new ConcurrentDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        HashSet<string>? supplied = null;
        foreach (KeyValuePair<string, FileDependency> directory in directoryStates)
        {
            if (_directories.TryGetValue(directory.Key, out Listing? stored) && stored.IsReadAt(directory.Value))
            {
                foreach (KeyValuePair<string, IReadOnlyList<string>> entry in stored.Entries)
                {
                    if (listings.TryAdd(entry.Key, entry.Value))
                    {
                        (supplied ??= new HashSet<string>(StringComparer.Ordinal)).Add(entry.Key);
                    }
                }
            }
        }

        return new Replay(this, directoryStates, listings, supplied);
    }

    private void Store(string directory, FileDependency state, List<KeyValuePair<string, IReadOnlyList<string>>> entries)
    {
        if (_directories.TryGetValue(directory, out Listing? existing) && existing.IsReadAt(state))
        {
            // The same state read more listing kinds this time, so keep what is already stored too.
            foreach (KeyValuePair<string, IReadOnlyList<string>> stored in existing.Entries)
            {
                if (!entries.Exists(entry => string.Equals(entry.Key, stored.Key, StringComparison.Ordinal)))
                {
                    entries.Add(stored);
                }
            }

            if (entries.Count == existing.Entries.Length)
            {
                return;
            }
        }
        else if (_directories.Count >= _maximumDirectories)
        {
            _directories.Clear();
        }

        _directories[directory] = new Listing(state, [.. entries]);
    }

    private sealed class Listing(FileDependency state, KeyValuePair<string, IReadOnlyList<string>>[] entries)
    {
        internal FileDependency State { get; } = state;

        internal KeyValuePair<string, IReadOnlyList<string>>[] Entries { get; } = entries;

        internal bool IsReadAt(FileDependency current) =>
            current.Kind == PathKind.Directory
            && State.LastWriteTimeUtc == current.LastWriteTimeUtc
            && State.Length == current.Length;
    }

    /// <summary>
    /// The listings one entry's replay shares, started from what is stored and finished by keeping what the replay read.
    /// </summary>
    internal sealed class Replay
    {
        private readonly ValidatedDirectoryListings _owner;
        private readonly IReadOnlyList<KeyValuePair<string, FileDependency>> _directoryStates;
        private readonly HashSet<string>? _supplied;

        internal Replay(
            ValidatedDirectoryListings owner,
            IReadOnlyList<KeyValuePair<string, FileDependency>> directoryStates,
            ConcurrentDictionary<string, IReadOnlyList<string>> listings,
            HashSet<string>? supplied)
        {
            _owner = owner;
            _directoryStates = directoryStates;
            Listings = listings;
            _supplied = supplied;
        }

        /// <summary>The directory-listing cache to hand to glob replay.</summary>
        internal ConcurrentDictionary<string, IReadOnlyList<string>> Listings { get; }

        /// <summary>How many listings came from the store instead of the file system.</summary>
        internal int Supplied => _supplied?.Count ?? 0;

        /// <summary>
        /// Keeps the listings the replay read from the file system. Call only after every glob matched, so a listing
        /// that produced a mismatch is never kept.
        /// </summary>
        internal void KeepReadListings(DateTime now)
        {
            DateTime newestTrusted = now - RacyTimestampWindow;
            Dictionary<string, FileDependency>? states = null;
            Dictionary<string, List<KeyValuePair<string, IReadOnlyList<string>>>>? byDirectory = null;
            foreach (KeyValuePair<string, IReadOnlyList<string>> listing in Listings)
            {
                // Keys are "F;", "D;" or "A;" followed by the directory, as the matcher builds them.
                if (listing.Key.Length < 3 || listing.Key[1] != ';' || (_supplied?.Contains(listing.Key) ?? false))
                {
                    continue;
                }

                states ??= ToLookup(_directoryStates);
                string directory = EvaluationInputRecorder.Canonicalize(listing.Key.Substring(2));
                if (!states.TryGetValue(directory, out FileDependency state)
                    || state.Kind != PathKind.Directory
                    || state.LastWriteTimeUtc > newestTrusted)
                {
                    continue;
                }

                byDirectory ??= new Dictionary<string, List<KeyValuePair<string, IReadOnlyList<string>>>>(FileUtilities.PathComparer);
                if (!byDirectory.TryGetValue(directory, out List<KeyValuePair<string, IReadOnlyList<string>>>? entries))
                {
                    byDirectory[directory] = entries = [];
                }

                entries.Add(listing);
            }

            if (byDirectory is null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<KeyValuePair<string, IReadOnlyList<string>>>> directory in byDirectory)
            {
                _owner.Store(directory.Key, states![directory.Key], directory.Value);
            }
        }

        private static Dictionary<string, FileDependency> ToLookup(IReadOnlyList<KeyValuePair<string, FileDependency>> directoryStates)
        {
            var lookup = new Dictionary<string, FileDependency>(directoryStates.Count, FileUtilities.PathComparer);
            foreach (KeyValuePair<string, FileDependency> directory in directoryStates)
            {
                lookup[directory.Key] = directory.Value;
            }

            return lookup;
        }
    }
}
