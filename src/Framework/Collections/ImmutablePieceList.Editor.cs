// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal sealed partial class ImmutablePieceList<T>
{
    /// <summary>
    ///  Computes a delta over a base snapshot using inherited ranges and newly owned item storage.
    /// </summary>
    /// <remarks>
    ///  creating a snapshot makes the new snapshot the next base; subsequent edits start with a single range of that base.
    ///  An unchanged editor needs no local piece list and returns its base directly from <see cref="ToImmutable"/>.
    ///  <para>
    ///   Item buffers are append-only, including across snapshot creation. A snapshot freezes the referenced cells,
    ///   while this editor may continue filling its buffer's unused tail. Editors branched from a snapshot start
    ///   with their own append storage, so they cannot overwrite each other's additions.
    ///  </para>
    ///  <para>
    ///   Logical edits invalidate active editor enumerators, but snapshot creation alone does not. Bulk callbacks may
    ///   read or publish the pre-edit sequence; reentrant edits are rejected before generated changes are committed.
    ///   The editor itself is not thread-safe.
    ///  </para>
    /// </remarks>
    public sealed class Editor : IReadOnlyList<T>
    {
        /// <summary>
        ///  The initial number of elements reserved in an editor's first append chunk.
        /// </summary>
        private const int InitialChunkCapacity = 4;

        /// <summary>
        ///  The element-capacity limit for geometrically growing append chunks.
        /// </summary>
        private const int MaximumChunkCapacity = 256;

        /// <summary>
        ///  The exclusively owned local delta, or <see langword="null"/> when the current state is the base snapshot.
        /// </summary>
        /// <remarks>
        ///  snapshot creation transfers this list to a snapshot and clears the field. The next edit creates a new
        ///  description containing one base range rather than copying the frozen list.
        /// </remarks>
        private List<Piece>? _pieces;

        /// <summary>
        ///  The immutable starting point for the current delta, always replaced by the most recently frozen state.
        /// </summary>
        private ImmutablePieceList<T> _baseSnapshot;

        /// <summary>
        ///  The current logical element count, which can differ from the base count while a delta is being edited.
        /// </summary>
        private int _count;

        /// <summary>
        ///  This editor's current append chunk, whose already written cells are never reused or overwritten.
        /// </summary>
        /// <remarks>
        ///  Published snapshots can reference earlier cells in this same array. Sharing those cells does not
        ///  transfer ownership of the unwritten tail to snapshots or to editors branched from them.
        /// </remarks>
        private T[]? _appendBuffer;

        /// <summary>
        ///  The first unwritten cell in the append buffer, including reservations consumed by failed bulk operations.
        /// </summary>
        private int _appendPosition;

        /// <summary>
        ///  The capacity to allocate when the current append chunk is absent or exhausted.
        /// </summary>
        private int _nextChunkCapacity = InitialChunkCapacity;

        /// <summary>
        ///  The logical-edit generation used to reject invalidated enumeration and reentrant bulk callbacks.
        /// </summary>
        /// <remarks>
        ///  creating a snapshot changes representation but not contents, so it does not increment this generation.
        /// </remarks>
        private int _version;

        /// <summary>
        ///  Creates an editor whose initial state is a single range of <paramref name="snapshot"/>.
        /// </summary>
        /// <param name="snapshot">The immutable starting sequence.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
        /// <remarks>
        ///  Construction does not copy descriptors or elements, and append storage is allocated only when needed.
        /// </remarks>
        public Editor(ImmutablePieceList<T> snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            _baseSnapshot = snapshot;
            _count = snapshot.Count;
        }

        /// <summary>
        ///  Gets the number of elements in the current sequence.
        /// </summary>
        public int Count => _count;

        /// <summary>
        ///  Gets the number of pieces in the current delta, treating an unchanged nonempty base as one piece.
        /// </summary>
        public int PieceCount => _pieces?.Count ?? (Count == 0 ? 0 : 1);

        /// <summary>
        ///  Gets or replaces the element at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based logical position.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the sequence.</exception>
        /// <remarks>
        ///  Reads validate the external position once before resolving source ranges. Replacement writes a new
        ///  cell and splices it into the delta; it never changes the old cell, even when the value compares equal.
        /// </remarks>
        public T this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

                List<Piece> pieces = _pieces ?? _baseSnapshot._pieces;
                Piece piece = pieces[FindPiece(pieces, index)];
                return GetItemRef(piece, index - (piece.End - piece.Length));
            }

            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

                EnsureAppendBuffer();
                int offset = _appendPosition++;
                _appendBuffer[offset] = value;
                ReplaceRange(index, 1, Piece.FromBuffer(_appendBuffer, offset, 1));
            }
        }

        /// <summary>
        ///  Appends <paramref name="item"/> without modifying any existing element storage.
        /// </summary>
        /// <param name="item">The element to append.</param>
        /// <exception cref="OverflowException">The resulting element count exceeds <see cref="int.MaxValue"/>.</exception>
        public void Add(T item)
        {
            int newCount = checked(Count + 1);
            EnsurePieces();
            EnsureAppendBuffer();
            int offset = _appendPosition++;
            _appendBuffer[offset] = item;
            AppendPiece(_pieces, Piece.FromBuffer(_appendBuffer, offset, 1));
            _count = newCount;
            _version++;
        }

        /// <summary>
        ///  Copies <paramref name="items"/> into owned append storage and appends their ranges.
        /// </summary>
        /// <param name="items">The elements to append.</param>
        /// <exception cref="OverflowException">The resulting element count exceeds <see cref="int.MaxValue"/>.</exception>
        /// <remarks>
        ///  Input backing storage is not retained. An empty input is a no-op and does not invalidate enumerators or
        ///  introduce a new snapshot. Adjacent additions within the same chunk form one piece.
        /// </remarks>
        public void AddRange(ReadOnlySpan<T> items)
        {
            int newCount = checked(Count + items.Length);
            if (items.IsEmpty)
            {
                return;
            }

            EnsurePieces();
            while (!items.IsEmpty)
            {
                EnsureAppendBuffer();
                int length = Math.Min(items.Length, _appendBuffer.Length - _appendPosition);
                items.Slice(0, length).CopyTo(_appendBuffer.AsSpan(_appendPosition, length));
                AppendPiece(_pieces, Piece.FromBuffer(_appendBuffer, _appendPosition, length));
                _appendPosition += length;
                items = items.Slice(length);
            }

            _count = newCount;
            _version++;
        }

        /// <summary>
        ///  Removes the element at <paramref name="index"/> while preserving survivor order.
        /// </summary>
        /// <param name="index">The zero-based logical position to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the sequence.</exception>
        public void RemoveAt(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            RemoveRangeCore(index, 1);
        }

        /// <summary>
        ///  Removes a logical range without copying the remaining elements.
        /// </summary>
        /// <param name="index">The zero-based start of the range.</param>
        /// <param name="count">The number of elements to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">The range is outside the sequence.</exception>
        /// <remarks>
        ///  An empty range is permitted at any valid insertion position, including <see cref="Count"/>.
        ///  Partial removal retains source slices; removing everything releases the editor's base and append storage.
        /// </remarks>
        public void RemoveRange(int index, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Count);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count - index);

            RemoveRangeCore(index, count);
        }

        /// <summary>
        ///  Removes all matching elements, preserving survivor order and sharing their storage.
        /// </summary>
        /// <param name="match">The predicate identifying elements to remove.</param>
        /// <returns>
        ///  The number of removed elements.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="match"/> modifies this editor.</exception>
        /// <remarks>
        ///  Matching is sequential and unchanged runs remain ranges of their original sources.
        ///  If the predicate throws, this method does not commit its removals. Side effects performed by the
        ///  predicate itself are not rolled back. With no matches, the current delta and snapshot identity are retained.
        /// </remarks>
        public int RemoveAll(Predicate<T> match)
        {
            ArgumentNullException.ThrowIfNull(match);
            return RewriteMatches(match, update: null);
        }

        /// <summary>
        ///  Replaces matching elements in one sequential traversal, sharing unchanged ranges.
        /// </summary>
        /// <param name="match">The predicate identifying elements to replace.</param>
        /// <param name="update">Computes the replacement for each matching element.</param>
        /// <returns>
        ///  The number of matching elements, including replacements equal to their original values.
        /// </returns>
        /// <exception cref="ArgumentNullException">Either callback is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">A callback modifies this editor.</exception>
        /// <remarks>
        ///  Matching visits elements in logical order exactly once, and <paramref name="update"/> is called only
        ///  for matching elements. Both callbacks see the pre-update sequence. They may read or publish it, but
        ///  may not perform reentrant edits.
        ///  <para>
        ///   Replacements are committed only after traversal completes. If either callback throws, this method
        ///   does not commit its replacements; callback side effects are not rolled back. Unchanged ranges are
        ///   retained without expanding their ancestor piece indices.
        ///  </para>
        ///  <para>
        ///   Returning an equal value still counts as a replacement. Objects referenced by elements must be
        ///   cloned by the caller before mutation; the collection does not provide deep immutability.
        ///  </para>
        /// </remarks>
        public int UpdateAll(Predicate<T> match, Func<T, T> update)
        {
            ArgumentNullException.ThrowIfNull(match);
            ArgumentNullException.ThrowIfNull(update);
            return RewriteMatches(match, update);
        }

        /// <summary>
        ///  Removes all elements and releases this editor's references to their storage.
        /// </summary>
        /// <remarks>
        ///  Existing snapshots and enumerators over those snapshots remain valid. The editor returns to the
        ///  shared empty base, with no ancestry or append chunks retained on its own behalf.
        /// </remarks>
        public void Clear()
        {
            if (Count == 0)
            {
                return;
            }

            _pieces = null;
            _baseSnapshot = Empty;
            _count = 0;
            _appendBuffer = null;
            _appendPosition = 0;
            _nextChunkCapacity = InitialChunkCapacity;
            _version++;
        }

        /// <summary>
        ///  Publishes the current delta without copying elements or inherited piece indices, then rebases the editor on it.
        /// </summary>
        /// <returns>
        ///  The same snapshot instance until the editor changes.
        /// </returns>
        /// <remarks>
        ///  A pending piece list is transferred to the snapshot, not copied. Clearing the local list makes the
        ///  frozen state the next base. Its buffers remain append-only even though this editor can continue
        ///  using their unwritten tail. snapshot creation does not invalidate active editor enumeration.
        /// </remarks>
        public ImmutablePieceList<T> ToImmutable()
        {
            if (_pieces is not null)
            {
                // The list is now frozen; later edits must describe ranges of this new base.
                _baseSnapshot = new ImmutablePieceList<T>(_pieces, Count);
                _pieces = null;
            }

            return _baseSnapshot;
        }

        /// <summary>
        ///  Gets an enumerator over the current sequence without creating a snapshot or copying pieces.
        /// </summary>
        /// <returns>
        ///  An enumerator invalidated by subsequent edits.
        /// </returns>
        /// <remarks>
        ///  Version and source references are captured when this method is called, before the iterator starts.
        ///  This permits snapshot creation during enumeration while still detecting edits before the first advance.
        /// </remarks>
        public IEnumerator<T> GetEnumerator() => Enumerate(_version, _pieces, _baseSnapshot, PieceCount);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        ///  Applies an already validated removal range without repeating public argument checks.
        /// </summary>
        /// <param name="index">The valid logical start of the range.</param>
        /// <param name="count">The valid number of elements to remove, possibly zero.</param>
        /// <remarks>
        ///  Empty ranges preserve the current state. Whole-sequence removal uses <see cref="Clear"/> so that
        ///  no unnecessary source snapshot or append storage remains attached to the editor.
        /// </remarks>
        private void RemoveRangeCore(int index, int count)
        {
            if (count == 0)
            {
                return;
            }

            if (count == Count)
            {
                Clear();
                return;
            }

            ReplaceRange(index, count, null);
        }

        /// <summary>
        ///  Builds a removal or replacement delta with one sequential traversal of the captured pre-edit sources.
        /// </summary>
        /// <param name="match">The validated predicate selecting elements to change.</param>
        /// <param name="update">The replacement callback, or <see langword="null"/> to remove matching elements.</param>
        /// <returns>
        ///  The number of matched elements.
        /// </returns>
        /// <remarks>
        ///  The output index is allocated lazily at the first match and is not installed until all callbacks finish.
        ///  Source-relative runs are retained as pieces, so even a heavily fragmented ancestor need not be expanded
        ///  into the local index. One cursor and its traversal stack are reused for every local input piece.
        ///  <para>
        ///   Generated replacement cells may consume append-buffer space before a callback throws. They remain
        ///   outside the logical sequence; reservations are not rewound or reused.
        ///  </para>
        /// </remarks>
        private int RewriteMatches(Predicate<T> match, Func<T, T>? update)
        {
            if (Count == 0)
            {
                return 0;
            }

            List<Piece>? rewritten = null;
            // Callbacks may publish and rebase the editor. Keep traversing the captured pre-edit description.
            List<Piece>? pieces = _pieces;
            ImmutablePieceList<T> snapshot = _baseSnapshot;
            int pieceCount = PieceCount;
            int matched = 0;
            int version = _version;
            using Enumerator enumerator = default;
            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                Piece piece = GetPiece(pieces, snapshot, pieceIndex);
                enumerator.ResetTo(piece.Snapshot, piece.Buffer, piece.Offset, piece.Length);
                // These offsets address the original piece, not the leaf buffers visited by the cursor.
                int runStart = 0;
                int offset = 0;
                while (enumerator.MoveNext())
                {
                    T item = enumerator.Current;
                    bool isMatch = match(item);
                    VerifyVersion(version);
                    if (isMatch)
                    {
                        if (rewritten is null)
                        {
                            // Nothing before the first match has changed, so its local descriptors can be reused.
                            rewritten = new List<Piece>(pieceCount);
                            for (int i = 0; i < pieceIndex; i++)
                            {
                                rewritten.Add(GetPiece(pieces, snapshot, i));
                            }
                        }

                        // Preserve the untouched run as a source slice rather than copying its elements.
                        AppendPiece(rewritten, piece with { Offset = piece.Offset + runStart, Length = offset - runStart });
                        if (update is not null)
                        {
                            T replacement = update(item);
                            VerifyVersion(version);
                            EnsureAppendBuffer();
                            int replacementOffset = _appendPosition++;
                            _appendBuffer[replacementOffset] = replacement;
                            AppendPiece(rewritten, Piece.FromBuffer(_appendBuffer, replacementOffset, 1));
                        }

                        runStart = offset + 1;
                        matched++;
                    }

                    offset++;
                }

                if (rewritten is not null)
                {
                    AppendPiece(rewritten, piece with { Offset = piece.Offset + runStart, Length = piece.Length - runStart });
                }
            }

            if (rewritten is null)
            {
                return 0;
            }

            if (update is null && matched == Count)
            {
                Clear();
            }
            else
            {
                // No generated change becomes visible through the editor until traversal has succeeded.
                _pieces = rewritten;
                if (update is null)
                {
                    _count -= matched;
                }

                _version++;
            }

            return matched;
        }

        /// <summary>
        ///  Enumerates a captured editor view while allowing snapshot creation but rejecting logical edits.
        /// </summary>
        /// <param name="version">The logical-edit generation at enumerator creation.</param>
        /// <param name="pieces">The captured local delta, or <see langword="null"/> for an unchanged base.</param>
        /// <param name="snapshot">The captured base used when <paramref name="pieces"/> is <see langword="null"/>.</param>
        /// <param name="pieceCount">The number of pieces in the captured view, including an implicit base range.</param>
        /// <returns>
        ///  The elements of the captured view in logical order.
        /// </returns>
        /// <remarks>
        ///  Version checks precede cursor advancement and access to borrowed descriptors. creating a snapshot may move
        ///  the editor to a new base, but it cannot alter the captured contents and does not change the generation.
        /// </remarks>
        private IEnumerator<T> Enumerate(int version, List<Piece>? pieces, ImmutablePieceList<T> snapshot, int pieceCount)
        {
            VerifyVersion(version);
            using Enumerator enumerator = default;
            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                VerifyVersion(version);
                Piece piece = GetPiece(pieces, snapshot, pieceIndex);
                enumerator.ResetTo(piece.Snapshot, piece.Buffer, piece.Offset, piece.Length);
                while (true)
                {
                    VerifyVersion(version);
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }

                    yield return enumerator.Current;
                }
            }

            VerifyVersion(version);
        }

        /// <summary>
        ///  Rejects edits made since an enumeration or bulk traversal captured its view.
        /// </summary>
        /// <param name="version">The expected logical-edit generation.</param>
        /// <exception cref="InvalidOperationException">The editor's logical contents have been edited.</exception>
        private void VerifyVersion(int version)
        {
            if (version != _version)
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        ///  Gets a local descriptor or synthesizes the unchanged editor's single full-base range.
        /// </summary>
        /// <param name="pieces">The local index, or <see langword="null"/> for an unchanged base.</param>
        /// <param name="snapshot">The base snapshot used when there is no local index.</param>
        /// <param name="index">A valid local piece index; zero for an implicit base range.</param>
        /// <returns>
        ///  The source-range description, without allocating or expanding the base snapshot's index.
        /// </returns>
        private static Piece GetPiece(List<Piece>? pieces, ImmutablePieceList<T> snapshot, int index)
            => pieces is not null ? pieces[index] : Piece.FromSnapshot(snapshot, 0, snapshot.Count);

        /// <summary>
        ///  Materializes an editable local index without copying the base snapshot's descriptors.
        /// </summary>
        /// <remarks>
        ///  A nonempty base starts as one source range. An empty base starts with an empty index.
        ///  This method is called only when an operation will edit the sequence, not for read-only access.
        /// </remarks>
        [MemberNotNull(nameof(_pieces))]
        private void EnsurePieces()
        {
            if (_pieces is null)
            {
                _pieces = Count == 0 ? [] : [Piece.FromSnapshot(_baseSnapshot, 0, Count)];
            }
        }

        /// <summary>
        ///  Supplies an append chunk with at least one unwritten cell.
        /// </summary>
        /// <remarks>
        ///  Exhausted chunks are replaced, never resized or copied, because snapshots may reference their cells.
        ///  Allocation grows geometrically up to <see cref="MaximumChunkCapacity"/> elements per chunk.
        /// </remarks>
        [MemberNotNull(nameof(_appendBuffer))]
        private void EnsureAppendBuffer()
        {
            if (_appendBuffer is null || _appendPosition == _appendBuffer.Length)
            {
                _appendBuffer = new T[_nextChunkCapacity];
                _appendPosition = 0;
                _nextChunkCapacity = Math.Min(MaximumChunkCapacity, _nextChunkCapacity * 2);
            }
        }

        /// <summary>
        ///  Appends a nonempty range, recomputing its logical end and merging a contiguous same-source predecessor.
        /// </summary>
        /// <param name="pieces">The exclusively owned output index.</param>
        /// <param name="piece">The source slice to append; an empty slice is ignored.</param>
        /// <remarks>
        ///  The incoming logical end is not authoritative: slicing preserves source coordinates, while the
        ///  output position follows the lengths of pieces already appended.
        /// </remarks>
        private static void AppendPiece(List<Piece> pieces, Piece piece)
        {
            if (piece.Length == 0)
            {
                return;
            }

            int end = pieces.Count == 0 ? 0 : pieces[pieces.Count - 1].End;
            if (pieces.Count > 0)
            {
                Piece previous = pieces[pieces.Count - 1];
                if (previous.HasSameSource(piece) && previous.Offset + previous.Length == piece.Offset)
                {
                    pieces[pieces.Count - 1] = previous with { Length = previous.Length + piece.Length, End = end + piece.Length };
                    return;
                }
            }

            pieces.Add(piece with { End = end + piece.Length });
        }

        /// <summary>
        ///  Splices a validated nonempty logical range into the local delta without expanding ancestor indices.
        /// </summary>
        /// <param name="index">The valid logical start of the edited range.</param>
        /// <param name="count">The positive number of existing elements covered by the edit.</param>
        /// <param name="replacement">The indexed assignment's replacement piece, or <see langword="null"/> for removal.</param>
        /// <remarks>
        ///  Only the first and last affected pieces can contribute surviving slices. Their offsets remain
        ///  relative to their original sources; their ends are positions in the edited logical sequence.
        ///  Whole-sequence removal is handled by <see cref="RemoveRangeCore"/> before reaching this helper.
        /// </remarks>
        private void ReplaceRange(int index, int count, Piece? replacement)
        {
            EnsurePieces();
            List<Piece> pieces = _pieces;
            int firstIndex = FindPiece(pieces, index);
            int lastIndex = FindPiece(pieces, index + count - 1);
            Piece first = pieces[firstIndex];
            Piece last = pieces[lastIndex];
            int prefixLength = index - (first.End - first.Length);
            int suffixLength = last.End - (index + count);
            int replacementLength = replacement?.Length ?? 0;
            int delta = replacementLength - count;

            // Retain boundary slices around the edit; all fully covered descriptors can be discarded.
            pieces.RemoveRange(firstIndex, lastIndex - firstIndex + 1);
            int insertionIndex = firstIndex;
            if (prefixLength > 0)
            {
                pieces.Insert(insertionIndex++, first with { Length = prefixLength, End = index });
            }

            if (replacement is Piece inserted)
            {
                pieces.Insert(insertionIndex++, inserted with { End = index + replacementLength });
            }

            if (suffixLength > 0)
            {
                pieces.Insert(
                    insertionIndex++,
                    last with { Offset = last.Offset + last.Length - suffixLength, Length = suffixLength, End = last.End + delta });
            }

            if (delta != 0)
            {
                // Same-length replacement leaves later logical ends unchanged.
                for (int i = insertionIndex; i < pieces.Count; i++)
                {
                    pieces[i] = pieces[i] with { End = pieces[i].End + delta };
                }
            }

            _count += delta;
            CoalescePieces(pieces, Math.Max(0, firstIndex - 1), Math.Min(pieces.Count - 1, insertionIndex));
            _version++;
        }

        /// <summary>
        ///  Merges adjacent, contiguous ranges of the same source near a splice boundary.
        /// </summary>
        /// <param name="pieces">The exclusively owned local index.</param>
        /// <param name="start">The first piece index to consider as a left-hand neighbor.</param>
        /// <param name="end">The last piece index in the initial merge window.</param>
        /// <remarks>
        ///  Only descriptors around the splice can have become newly mergeable. A merged range keeps the
        ///  right piece's logical end, so later descriptors need no position adjustment.
        /// </remarks>
        private static void CoalescePieces(List<Piece> pieces, int start, int end)
        {
            for (int i = start; i < end;)
            {
                Piece left = pieces[i];
                Piece right = pieces[i + 1];
                if (left.HasSameSource(right) && left.Offset + left.Length == right.Offset)
                {
                    pieces[i] = left with { Length = left.Length + right.Length, End = right.End };
                    pieces.RemoveAt(i + 1);
                    // Revisit the enlarged left range against its new neighbor.
                    end--;
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
