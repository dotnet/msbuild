// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.Globbing;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared;

internal partial class FileMatcher
{
    /// <summary>
    /// Expands sibling specifications with separate results while sharing overlapping directory traversals.
    /// Instances belong to one item operation, not to the evaluation context.
    /// </summary>
    internal sealed partial class GlobExpander(
        FileMatcher fileMatcher,
        string? projectDirectory,
        IReadOnlyList<string> specifications,
        List<string>? excludes)
    {
        private List<Entry>? _entries;
        private bool[]? _completedSpecifications;

        internal void NotifyCompleted(string specification)
        {
            if (_entries is null)
            {
                _completedSpecifications ??= new bool[specifications.Count];
                for (int index = 0; index < specifications.Count; index++)
                {
                    if (string.Equals(specifications[index], specification, StringComparison.Ordinal))
                    {
                        _completedSpecifications[index] = true;
                    }
                }
            }
            else if (FindEntry(specification) is { } entry)
            {
                entry.Matcher = null;
                entry.Files = null;
            }
        }

        internal (string[] FileList, SearchAction Action, string ExcludeFileSpec, string? GlobFailure) GetFiles(
            string specification,
            DriverSelection selection)
        {
            Initialize();

            Entry? entry = FindEntry(specification);
            if (entry is not null)
            {
                if (entry.Files is null && entry.Matcher is not null)
                {
                    ExpandGroup(entry);
                }

                if (entry.Files is not null)
                {
                    return (entry.Files, SearchAction.RunSearch, string.Empty, null);
                }
            }

            return fileMatcher.GetFilesForImplementation(projectDirectory, specification, excludes, selection);
        }

        private Entry? FindEntry(string specification)
        {
            foreach (Entry entry in _entries!)
            {
                if (string.Equals(entry.Specification, specification, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private void Initialize()
        {
            if (_entries is not null)
            {
                return;
            }

            _entries = new List<Entry>(specifications.Count);
            for (int index = 0; index < specifications.Count; index++)
            {
                string specification = specifications[index];
                if (_completedSpecifications?[index] == true || FindEntry(specification) is not null)
                {
                    continue;
                }

                Entry entry = new(specification);
                _entries.Add(entry);

                // Cached callbacks have their own filename semantics. Other backends keep their existing driver.
                if (!HasWildcards(specification)
                    || !fileMatcher._usesFileSystemEntryCache
                    || string.IsNullOrEmpty(projectDirectory))
                {
                    continue;
                }

                DriverSelection selection = fileMatcher.SelectDriver(projectDirectory, specification, excludes);
                if (selection.Driver == FileMatcherDriver.Legacy
                    && selection.FallbackReason != FileMatcherFallbackReason.CacheBackedWithoutApplicableExcludes)
                {
                    continue;
                }

                fileMatcher.GetFilesOptimizedImplementation(
                    projectDirectory,
                    specification,
                    excludes,
                    FileMatcherDriver.OptimizedCallback,
                    (state, stripProjectDirectory, matcher, wildcardExcludes, literalExcludes) =>
                    {
                        entry.State = state;
                        entry.StripProjectDirectory = stripProjectDirectory;
                        entry.Matcher = matcher;
                        entry.Excludes = wildcardExcludes;
                        entry.LiteralExcludes = literalExcludes;
                    });
            }

            _completedSpecifications = null;
        }

        private void ExpandGroup(Entry requested)
        {
            string root = requested.State.BaseDirectory;
            foreach (Entry entry in _entries!)
            {
                if (entry.Files is null && entry.Matcher is not null
                    && CanShareTraversal(entry.State.BaseDirectory, root))
                {
                    root = entry.State.BaseDirectory;
                }
            }

            List<Entry> group = [];
            foreach (Entry entry in _entries)
            {
                if (entry.Files is null && entry.Matcher is not null
                    && CanShareTraversal(root, entry.State.BaseDirectory))
                {
                    group.Add(entry);
                }
            }

            if (group.Count < 2)
            {
                requested.Matcher = null;
                return;
            }

            Search[] searches = CreateSearches(group);
            try
            {
                if (CanVisitDirectory(root, searches))
                {
                    EnumerateGroup(root, searches);
                }
            }
            catch (Exception exception) when (IsIoRelatedExceptionOrAggregate(exception))
            {
                // Retry individually so a failure in one subtree does not change another glob's results or diagnostics.
                foreach (Entry entry in group)
                {
                    entry.Matcher = null;
                }

                return;
            }

            for (int index = 0; index < group.Count; index++)
            {
                if (searches[index].RootReached && !HasAmbiguousSortOrder(searches[index].Files))
                {
                    group[index].Files = [.. searches[index].Files];
                }
                else
                {
                    // Keep the original walk for unreached roots or case-insensitive sort ties.
                    group[index].Matcher = null;
                }
            }
        }

        private Search[] CreateSearches(List<Entry> group)
        {
            Search[] searches = new Search[group.Count];
            for (int index = 0; index < searches.Length; index++)
            {
                searches[index] = new Search(group[index], projectDirectory!);
            }

            return searches;
        }

        private static bool HasAmbiguousSortOrder(List<string> files) =>
            files.Count > 1 && new HashSet<string>(files, StringComparer.OrdinalIgnoreCase).Count != files.Count;

        private void EnumerateGroup(string root, Search[] searches)
        {
            int maxDegree = Math.Max(1, NativeMethods.GetLogicalCoreCount() / 2);
            List<string> pending = [root];
            int first = 0;

            // Look past common parent directories without serially preparing an unbounded tree.
            while (first < pending.Count && first < maxDegree && pending.Count - first < maxDegree)
            {
                EnumerateDirectory(pending[first++], searches, pending);
            }

            int degree = Math.Min(pending.Count - first, maxDegree);
            if (degree < 2)
            {
                for (int index = first; index < pending.Count; index++)
                {
                    EnumerateDirectory(pending[index], searches);
                }

                return;
            }

            Search[]?[] partitions = new Search[]?[degree];
            Parallel.For(0, degree, partition =>
            {
                Search[] localSearches = CreateSearchesForPartition(searches);
                for (int index = first + partition; index < pending.Count; index += degree)
                {
                    EnumerateDirectory(pending[index], localSearches);
                }

                partitions[partition] = localSearches;
            });

            foreach (Search[]? partition in partitions)
            {
                if (partition is not null)
                {
                    for (int index = 0; index < searches.Length; index++)
                    {
                        searches[index].Files.AddRange(partition[index].Files);
                        searches[index].RootReached |= partition[index].RootReached;
                    }
                }
            }
        }

        private void EnumerateDirectory(string directory, Search[] searches, List<string>? pending = null)
        {
            if (ShouldSkipRecursiveDirectory(directory))
            {
                return;
            }

            using BufferScope<int> fileSearches = new(stackalloc int[8], searches.Length);
            int fileSearchCount = 0;
            bool recurse = false;
            for (int index = 0; index < searches.Length; index++)
            {
                if (searches[index].BeginDirectory(directory, out bool canMatchDescendants))
                {
                    fileSearches[fileSearchCount++] = index;
                }

                recurse |= canMatchDescendants;
            }

            if (fileSearchCount > 0)
            {
                IReadOnlyList<string> files = fileMatcher._getFileSystemEntries(
                    FileSystemEntity.Files, directory, null, projectDirectory, false);
                foreach (string file in files)
                {
                    // A file can belong to several fragments, each with its own output path and provenance.
                    for (int index = 0; index < fileSearchCount; index++)
                    {
                        searches[fileSearches[index]].AddFile(file);
                    }
                }
            }

            if (recurse)
            {
                IReadOnlyList<string> subdirectories = fileMatcher._getFileSystemEntries(
                    FileSystemEntity.Directories, directory, null, null, false);
                foreach (string subdirectory in subdirectories)
                {
                    if (CanVisitDirectory(subdirectory, searches))
                    {
                        if (pending is not null)
                        {
                            pending.Add(subdirectory);
                        }
                        else
                        {
                            EnumerateDirectory(subdirectory, searches);
                        }
                    }
                }
            }
        }

        private static bool CanVisitDirectory(string directory, Search[] searches)
        {
            foreach (Search search in searches)
            {
                if (search.MatchesDirectory(directory) != DirectoryMatchType.NoDescendantFilesMatch)
                {
                    return true;
                }
            }

            return false;
        }

        private static Search[] CreateSearchesForPartition(Search[] searches)
        {
            Search[] partition = new Search[searches.Length];
            for (int index = 0; index < partition.Length; index++)
            {
                partition[index] = new Search(searches[index].Entry, searches[index].ProjectDirectory);
            }

            return partition;
        }

        private static bool ContainsRoot(string parent, string child) =>
            string.Equals(parent, child, FileUtilities.PathComparison)
            || IsSubdirectoryOf(child, parent, FileUtilities.PathComparison);

        private static bool CanShareTraversal(string parent, string child) =>
            ContainsRoot(parent, child)
            // Relative segments in the suffix cannot be reached by enumerating child directories.
            && (parent.Length == child.Length
                || !FileUtilities.ContainsRelativePathSegments(child.Substring(parent.Length)));

        private static bool TryGetRelativeIncludeDirectory(
            string baseDirectory,
            ReadOnlySpan<char> directory,
            out ReadOnlySpan<char> relativeDirectory)
        {
            ReadOnlySpan<char> baseSpan = baseDirectory.AsSpan();
            while (baseSpan.Length > 1 && FileUtilities.IsSlash(baseSpan[^1]))
            {
                baseSpan = baseSpan[..^1];
            }

            if (directory.Equals(baseSpan, StringComparison.Ordinal))
            {
                relativeDirectory = default;
                return true;
            }

            if (baseSpan.IsEmpty
                || !directory.StartsWith(baseSpan, StringComparison.Ordinal)
                || directory.Length <= baseSpan.Length
                || (!FileUtilities.IsSlash(baseSpan[^1]) && !FileUtilities.IsSlash(directory[baseSpan.Length])))
            {
                relativeDirectory = default;
                return false;
            }

            int relativeStart = baseSpan.Length;
            while (relativeStart < directory.Length && FileUtilities.IsSlash(directory[relativeStart]))
            {
                relativeStart++;
            }

            relativeDirectory = directory[relativeStart..];
            return true;
        }

        private sealed class Entry(string specification)
        {
            internal string Specification { get; } = specification;
            internal string[]? Files;
            internal RecursionState State;
            internal bool StripProjectDirectory;
            internal MSBuildPathMatcher? Matcher;
            internal List<OptimizedFileSearch>? Excludes;
            internal HashSet<string>? LiteralExcludes;
        }

        private sealed class Search(Entry entry, string projectDirectory)
        {
            private readonly bool[] _activeExcludes = new bool[entry.Excludes?.Count ?? 0];
            private readonly MSBuildPathMatcher _matcher = entry.Matcher!;
            private readonly bool _projectDirectoryEndsInSeparator = IsDirectorySeparator(projectDirectory[projectDirectory.Length - 1]);
            private readonly string? _filePattern = entry.State.SearchData.Filespec is { } pattern && !IsAllFilesWildcard(pattern)
                ? pattern
                : null;
            private string? _outputDirectory;
            private int _relativeDirectoryStart;
            private string _directory = string.Empty;

            internal Entry Entry { get; } = entry;
            internal string ProjectDirectory { get; } = projectDirectory;
            internal List<string> Files { get; } = [];
            internal bool RootReached;

            internal bool BeginDirectory(string directory, out bool canMatchDescendants)
            {
                _directory = directory;
                if (!TryGetRelativeIncludeDirectory(Entry.State.BaseDirectory, directory, out ReadOnlySpan<char> relativeDirectory))
                {
                    canMatchDescendants = ContainsRoot(directory, Entry.State.BaseDirectory);
                    return false;
                }

                RootReached |= relativeDirectory.IsEmpty;
                canMatchDescendants = _matcher.CanMatchDescendants(relativeDirectory);
                if (!_matcher.MatchesFilesInDirectory(relativeDirectory))
                {
                    return false;
                }

                _outputDirectory = null;
                _relativeDirectoryStart = directory.Length - relativeDirectory.Length;
                for (int index = 0; index < _activeExcludes.Length; index++)
                {
                    OptimizedFileSearch exclude = Entry.Excludes![index];
                    _activeExcludes[index] = TryGetRelativeDirectory(
                        exclude.BaseDirectory, directory, out ReadOnlySpan<char> excludeDirectory)
                        && exclude.Matcher.MatchesFilesInDirectory(excludeDirectory);
                }

                return true;
            }

            internal void AddFile(string file)
            {
                if ((_filePattern is not null && !IsFileNameMatch(file, _filePattern))
                    || !_matcher.MatchesFileName(file))
                {
                    return;
                }

                for (int index = 0; index < _activeExcludes.Length; index++)
                {
                    if (_activeExcludes[index] && Entry.Excludes![index].Matcher.MatchesFileName(file))
                    {
                        return;
                    }
                }

                _outputDirectory ??= _relativeDirectoryStart == _directory.Length
                    ? Entry.State.BaseDirectory
                    : Path.Combine(Entry.State.BaseDirectory, _directory.Substring(_relativeDirectoryStart));
                string output = string.Equals(_directory, _outputDirectory, StringComparison.Ordinal)
                    ? file
                    : Path.Combine(_outputDirectory, Path.GetFileName(file));
                if (Entry.StripProjectDirectory)
                {
                    output = RemoveProjectDirectory(output, ProjectDirectory, _projectDirectoryEndsInSeparator);
                }

                if (Entry.LiteralExcludes?.Contains(output) != true)
                {
                    Files.Add(output);
                }
            }

            internal DirectoryMatchType MatchesDirectory(string directory)
            {
                if (!TryGetRelativeIncludeDirectory(Entry.State.BaseDirectory, directory, out ReadOnlySpan<char> relativeDirectory))
                {
                    return ContainsRoot(directory, Entry.State.BaseDirectory)
                        ? DirectoryMatchType.MayContainMatchingFiles
                        : DirectoryMatchType.NoDescendantFilesMatch;
                }

                DirectoryMatchType result = _matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, relativeDirectory);
                if (result == DirectoryMatchType.NoDescendantFilesMatch)
                {
                    return result;
                }

                if (Entry.Excludes is not null)
                {
                    foreach (OptimizedFileSearch exclude in Entry.Excludes)
                    {
                        if (TryGetRelativeDirectory(exclude.BaseDirectory, directory, out ReadOnlySpan<char> excludeDirectory))
                        {
                            result = DirectoryMatchTypeOperations.And(
                                result,
                                DirectoryMatchTypeOperations.Not(exclude.Matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, excludeDirectory)));
                        }
                    }
                }

                return result;
            }
        }
    }
}
