// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Collects ordinary Remove glob patterns in declaration order for one item type.
    /// </summary>
    /// <remarks>
    ///  Pattern storage is append-only and segmented. Cache identity uses occurrence positions without
    ///  constructing sets. Filesystem expansion can request one independently owned, read-only set.
    /// </remarks>
    private sealed class RemoveGlobPatternSequence
    {
        private readonly SegmentedList<RemovedGlob> _patterns = [];
        private Dictionary<string, int>? _lastOccurrences;
        private int _indexedPatternCount;
        private (int Start, int End, int Count, int HashCode) _lastContent;
        private HashSet<string>? _lastPatterns;
        private int _lastStart;
        private int _lastEnd;

        /// <summary>
        ///  Gets the number of pattern sets cached for filesystem expansion.
        /// </summary>
        public int CachedRangeCount => _lastPatterns is null ? 0 : 1;

        /// <summary>
        ///  Gets the total number of pattern sets constructed for filesystem expansion.
        /// </summary>
        public int CreatedSetCount { get; private set; }

        /// <summary>
        ///  Collects one pattern with the number of operations through its Remove declaration.
        /// </summary>
        /// <param name="pattern">The parsed, unescaped glob pattern.</param>
        /// <param name="operationCount">The position immediately after the Remove operation.</param>
        public void Add(string pattern, int operationCount)
            => _patterns.Add(new(pattern, operationCount));

        /// <summary>
        ///  Finds the exclusive pattern boundary for a requested operation sequence.
        /// </summary>
        /// <param name="operationCount">The number of operations visible to the request.</param>
        /// <returns>
        ///  The number of patterns declared within those operations, excluding all later declarations.
        /// </returns>
        public int GetPatternCount(int operationCount)
        {
            int end = _patterns.Count;
            if (end == 0 || _patterns[^1].OperationCount <= operationCount)
            {
                return end;
            }

            int start = 0;
            while (start < end)
            {
                int middle = start + ((end - start) / 2);
                if (_patterns[middle].OperationCount <= operationCount)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle;
                }
            }

            return start;
        }

        /// <summary>
        ///  Extends a backward-planning range to include this operation's patterns.
        /// </summary>
        /// <param name="start">The range's current starting boundary.</param>
        /// <param name="operationCount">The position immediately after the operation being visited.</param>
        /// <returns>
        ///  The starting boundary including all patterns from that operation.
        /// </returns>
        public int IncludePatternsFromOperation(int start, int operationCount)
        {
            while (start > 0 && _patterns[start - 1].OperationCount == operationCount)
            {
                start--;
            }

            return start;
        }

        /// <summary>
        ///  Removes this operation's patterns from a forward-evaluation range before applying it.
        /// </summary>
        /// <param name="start">The range's current starting boundary.</param>
        /// <param name="end">The requested sequence's exclusive pattern boundary.</param>
        /// <param name="operationCount">The position immediately after the operation being applied.</param>
        /// <returns>
        ///  The starting boundary containing only patterns from later operations.
        /// </returns>
        public int SkipPatternsFromOperation(int start, int end, int operationCount)
        {
            while (start < end && _patterns[start].OperationCount == operationCount)
            {
                start++;
            }

            return start;
        }

        /// <summary>
        ///  Identifies a bounded range of later Remove glob patterns without building its set.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  The later Remove glob patterns in the range, or an empty range.
        /// </returns>
        public RemoveGlobPatterns GetRange(int start, int end)
        {
            Debug.Assert(start >= 0 && start <= end && end <= _patterns.Count);
            return start == end ? default : new(this, start, end);
        }

        /// <summary>
        ///  Gets a range's patterns for filesystem expansion, caching only the most recent set.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  A privately owned pattern set that callers must not mutate.
        /// </returns>
        public HashSet<string> GetPatterns(int start, int end)
        {
            if (_lastPatterns is not null && _lastStart == start && _lastEnd == end)
            {
                return _lastPatterns;
            }

            HashSet<string> patterns = new(FileUtilities.PathComparer);
            for (int i = start; i < end; i++)
            {
                patterns.Add(_patterns[i].Pattern);
            }

            _lastPatterns = patterns;
            _lastStart = start;
            _lastEnd = end;
            CreatedSetCount++;
            return patterns;
        }

        /// <summary>
        ///  Compares the distinct patterns in two bounded ranges without constructing sets.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <param name="other">The sequence owning the other range.</param>
        /// <param name="otherStart">The other range's inclusive pattern boundary.</param>
        /// <param name="otherEnd">The other range's exclusive pattern boundary.</param>
        /// <returns>
        ///  Whether the ranges contain the same patterns under the filesystem path comparer.
        /// </returns>
        public bool ContentEquals(int start, int end, RemoveGlobPatternSequence other, int otherStart, int otherEnd)
        {
            var left = GetContent(start, end);
            var right = other.GetContent(otherStart, otherEnd);
            if (left.HashCode != right.HashCode || left.Count != right.Count)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                RemovedGlob pattern = _patterns[i];
                if (pattern.PreviousOccurrence < start && !other.Contains(pattern.Pattern, otherStart, otherEnd))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///  Gets an order-independent content hash without constructing a pattern set.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  A hash of the range's distinct patterns under the filesystem path comparer.
        /// </returns>
        public int GetContentHashCode(int start, int end) => GetContent(start, end).HashCode;

        private (int Count, int HashCode) GetContent(int start, int end)
        {
            if (_lastContent.Start == start && _lastContent.End == end)
            {
                return (_lastContent.Count, _lastContent.HashCode);
            }

            IndexPatterns(end);
            int count = 0;
            int hashCode = 0;
            for (int i = start; i < end; i++)
            {
                RemovedGlob pattern = _patterns[i];
                // Only the first occurrence within this range contributes to its set contents.
                if (pattern.PreviousOccurrence < start)
                {
                    count++;
                    hashCode ^= pattern.HashCode;
                }
            }

            hashCode ^= count;
            _lastContent = (start, end, count, hashCode);
            return (count, hashCode);
        }

        private bool Contains(string pattern, int start, int end)
        {
            if (!IndexPatterns(end).TryGetValue(pattern, out int occurrence))
            {
                return false;
            }

            // Later appends can move the index head, but cannot change a bounded range's contents.
            while (occurrence >= end)
            {
                occurrence = _patterns[occurrence].PreviousOccurrence;
            }

            return occurrence >= start;
        }

        private Dictionary<string, int> IndexPatterns(int end)
        {
            Dictionary<string, int> occurrences = _lastOccurrences ??= new(FileUtilities.PathComparer);
            while (_indexedPatternCount < end)
            {
                int index = _indexedPatternCount;
                RemovedGlob pattern = _patterns[index];
                int previous = occurrences.TryGetValue(pattern.Pattern, out int existing) ? existing : -1;
                _patterns[index] = pattern with
                {
                    PreviousOccurrence = previous,
                    HashCode = previous >= 0 ? _patterns[previous].HashCode : FileUtilities.PathComparer.GetHashCode(pattern.Pattern),
                };
                occurrences[pattern.Pattern] = index;
                _indexedPatternCount++;
            }

            return occurrences;
        }

        private readonly record struct RemovedGlob(string Pattern, int OperationCount, int PreviousOccurrence = -1, int HashCode = 0);
    }
}
