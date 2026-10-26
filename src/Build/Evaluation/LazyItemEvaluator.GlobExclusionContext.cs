// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies the Remove glob patterns relevant to one item-state computation.
    /// </summary>
    /// <remarks>
    ///  The range is bounded when created, so later declarations cannot change it. The default value
    ///  represents no exclusions. Sets and hashes are computed only when content is requested.
    /// </remarks>
    private readonly struct GlobExclusionContext : IEquatable<GlobExclusionContext>
    {
        private static readonly HashSet<string> s_emptyPatterns = new(FileUtilities.PathComparer);

        private readonly GlobExclusionHistory? _history;
        private readonly int _start;
        private readonly int _end;

        public GlobExclusionContext(GlobExclusionHistory history, int start, int end)
        {
            _history = history;
            _start = start;
            _end = end;
        }

        /// <summary>
        ///  Gets whether this range contains no Remove glob patterns.
        /// </summary>
        [MemberNotNullWhen(false, nameof(_history))]
        public bool IsEmpty => _history is null || _start == _end;

        /// <summary>
        ///  Gets the deduplicated patterns, constructing them only when needed.
        /// </summary>
        /// <returns>
        ///  The privately owned set, which callers must not mutate.
        /// </returns>
        public HashSet<string> GetPatterns()
            => IsEmpty ? s_emptyPatterns : _history.GetPatterns(_start, _end);

        /// <summary>
        ///  Retains this range's contents after it becomes a stored item-cache key.
        /// </summary>
        public void Retain()
        {
            if (!IsEmpty)
            {
                _history.RetainRange(_start, _end);
            }
        }

        public bool Equals(GlobExclusionContext other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return IsEmpty && other.IsEmpty;
            }

            if (ReferenceEquals(_history, other._history) && _start == other._start && _end == other._end)
            {
                return true;
            }

            var left = _history.GetMaterializedRange(_start, _end);
            var right = other._history.GetMaterializedRange(other._start, other._end);
            return left.HashCode == right.HashCode
                && left.Patterns.Count == right.Patterns.Count
                && left.Patterns.SetEquals(right.Patterns);
        }

        public override bool Equals(object? obj) => obj is GlobExclusionContext other && Equals(other);

        public override int GetHashCode()
            => IsEmpty ? 0 : _history.GetMaterializedRange(_start, _end).HashCode;
    }
}
