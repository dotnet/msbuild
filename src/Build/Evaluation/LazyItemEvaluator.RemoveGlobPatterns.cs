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
    ///  represents no exclusions. Cache hashing and equality do not construct pattern sets.
    /// </remarks>
    private readonly struct RemoveGlobPatterns : IEquatable<RemoveGlobPatterns>
    {
        private static readonly HashSet<string> s_emptyPatterns = new(FileUtilities.PathComparer);

        private readonly RemoveGlobPatternSequence? _sequence;
        private readonly int _start;
        private readonly int _end;

        public RemoveGlobPatterns(RemoveGlobPatternSequence sequence, int start, int end)
        {
            _sequence = sequence;
            _start = start;
            _end = end;
        }

        /// <summary>
        ///  Gets whether this range contains no Remove glob patterns.
        /// </summary>
        [MemberNotNullWhen(false, nameof(_sequence))]
        public bool IsEmpty => _sequence is null || _start == _end;

        /// <summary>
        ///  Gets the deduplicated patterns, constructing them only when needed.
        /// </summary>
        /// <returns>
        ///  The privately owned set, which callers must not mutate.
        /// </returns>
        public HashSet<string> GetPatterns()
            => IsEmpty ? s_emptyPatterns : _sequence.GetPatterns(_start, _end);

        public bool Equals(RemoveGlobPatterns other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return IsEmpty && other.IsEmpty;
            }

            if (ReferenceEquals(_sequence, other._sequence) && _start == other._start && _end == other._end)
            {
                return true;
            }

            return _sequence.ContentEquals(_start, _end, other._sequence, other._start, other._end);
        }

        public override bool Equals(object? obj) => obj is RemoveGlobPatterns other && Equals(other);

        public override int GetHashCode()
            => IsEmpty ? 0 : _sequence.GetContentHashCode(_start, _end);
    }
}
