// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal sealed partial class ImmutablePieceList<T>
{
    /// <summary>
    ///  Enumerates snapshot and buffer ranges using an explicit stack rather than recursive ancestor traversal.
    /// </summary>
    /// <remarks>
    ///  Snapshot positions and buffer positions are different coordinate systems. A range is resolved until
    ///  a buffer is reached, then its cells are yielded directly. Only unfinished parent ranges require stack
    ///  frames, so descending through an ancestor that has no remaining work does not grow the stack.
    ///  <para>
    ///   The parent stack is allocated lazily and reused by reset operations, including the internal range resets
    ///   used by editor bulk traversal.
    ///  </para>
    /// </remarks>
    public struct Enumerator : IEnumerator<T>
    {
        /// <summary>
        ///  The source range restored by <see cref="Reset"/>, or an empty piece after disposal.
        /// </summary>
        private Piece _root;

        /// <summary>
        ///  Deferred parent ranges, each operation collection its source, next piece, next logical position, and exclusive end.
        /// </summary>
        private Stack<(ImmutablePieceList<T> Snapshot, int PieceIndex, int Position, int End)>? _parents;

        /// <summary>
        ///  The active snapshot range, or <see langword="null"/> when only a resolved buffer or deferred parents remain.
        /// </summary>
        private ImmutablePieceList<T>? _snapshot;

        /// <summary>
        ///  The next descriptor to visit in the active snapshot's local index.
        /// </summary>
        private int _pieceIndex;

        /// <summary>
        ///  The next logical position in the active snapshot range.
        /// </summary>
        private int _position;

        /// <summary>
        ///  The exclusive logical end of the active snapshot range, possibly before the snapshot's own end.
        /// </summary>
        private int _end;

        /// <summary>
        ///  The resolved leaf array currently supplying elements.
        /// </summary>
        private T[]? _buffer;

        /// <summary>
        ///  The next array index to yield from the resolved buffer.
        /// </summary>
        private int _bufferPosition;

        /// <summary>
        ///  The exclusive array bound of the resolved range, not necessarily the buffer's length.
        /// </summary>
        private int _bufferEnd;

        /// <summary>
        ///  The last yielded value, valid only while <see cref="_hasCurrent"/> is <see langword="true"/>.
        /// </summary>
        private T _current;

        /// <summary>
        ///  Distinguishes a yielded <see langword="default"/> or <see langword="null"/> value from an unpositioned cursor.
        /// </summary>
        private bool _hasCurrent;

        /// <summary>
        ///  Creates a cursor over an entire immutable snapshot without copying its descriptors or elements.
        /// </summary>
        /// <param name="list">The snapshot to enumerate.</param>
        internal Enumerator(ImmutablePieceList<T> list)
        {
            this = default;
            _current = default!;
            ResetTo(list, null, 0, list.Count);
        }

        /// <summary>
        ///  Gets the value produced by the most recent successful advance.
        /// </summary>
        /// <value>
        ///  The current element, including a stored <see langword="default"/> or <see langword="null"/> value.
        /// </value>
        /// <exception cref="InvalidOperationException">The cursor has not advanced, was reset, is exhausted, or was disposed.</exception>
        public readonly T Current => _hasCurrent ? _current : throw new InvalidOperationException();

        readonly object? IEnumerator.Current => Current;

        /// <summary>
        ///  Advances through source ranges until another buffer cell is available.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if another element was yielded; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Once a buffer range is resolved, subsequent elements require no piece searches until that range ends.
        ///  Snapshot ranges are traversed depth-first using deferred parent continuations rather than recursion.
        /// </remarks>
        public bool MoveNext()
        {
            // The common element path stays in the current buffer; descriptor traversal happens only at its end.
            while (_bufferPosition == _bufferEnd)
            {
                ImmutablePieceList<T>? snapshot = _snapshot;
                if (snapshot is null)
                {
                    if (_parents is not { Count: > 0 } parents)
                    {
                        _buffer = null;
                        _current = default!;
                        _hasCurrent = false;
                        return false;
                    }

                    // The child is exhausted. Resume its parent's next unvisited piece without another search.
                    var parent = parents.Pop();
                    snapshot = parent.Snapshot;
                    _snapshot = snapshot;
                    _pieceIndex = parent.PieceIndex;
                    _position = parent.Position;
                    _end = parent.End;
                }

                Piece piece = snapshot._pieces[_pieceIndex];
                int offset = _position - (piece.End - piece.Length);
                // A reference can start or end inside a source piece, so honor both sets of boundaries.
                int length = Math.Min(piece.Length - offset, _end - _position);
                // Record the continuation before descending into a child or yielding from a leaf.
                _position += length;
                _pieceIndex++;
                if (_position == _end)
                {
                    _snapshot = null;
                }

                if (piece.Snapshot is { } source)
                {
                    // A fully consumed parent needs no frame, even in a long single-range ancestor chain.
                    if (_snapshot is { } remainder)
                    {
                        (_parents ??= new()).Push((remainder, _pieceIndex, _position, _end));
                    }

                    SetSnapshotRange(source, piece.Offset + offset, length);
                }
                else
                {
                    _buffer = piece.Buffer!;
                    _bufferPosition = piece.Offset + offset;
                    _bufferEnd = _bufferPosition + length;
                }
            }

            _current = _buffer![_bufferPosition++];
            _hasCurrent = true;
            return true;
        }

        /// <summary>
        ///  Restarts the currently configured root range and invalidates <see cref="Current"/>.
        /// </summary>
        /// <remarks>
        ///  Pending source references are cleared, but allocated stack capacity is retained for reuse.
        ///  After <see cref="Dispose"/> has discarded the root, resetting leaves an empty cursor.
        /// </remarks>
        public void Reset()
        {
            _parents?.Clear();
            _snapshot = null;
            _buffer = null;
            _bufferPosition = 0;
            _bufferEnd = 0;
            _current = default!;
            _hasCurrent = false;
            if (_root.Length == 0)
            {
                return;
            }

            if (_root.Snapshot is { } source)
            {
                SetSnapshotRange(source, _root.Offset, _root.Length);
            }
            else
            {
                _buffer = _root.Buffer!;
                _bufferPosition = _root.Offset;
                _bufferEnd = _root.Offset + _root.Length;
            }
        }

        /// <summary>
        ///  Discards the root and releases references to current elements, buffers, and ancestor snapshots.
        /// </summary>
        /// <remarks>
        ///  No item buffers are freed or returned to a pool: they belong to snapshots and editors.
        ///  Repeated disposal is harmless, and advancing afterward returns <see langword="false"/>.
        /// </remarks>
        public void Dispose()
        {
            _root = default;
            Reset();
        }

        /// <summary>
        ///  Reconfigures this cursor for a validated snapshot or buffer range.
        /// </summary>
        /// <param name="snapshot">The immutable source, or <see langword="null"/> for a buffer range.</param>
        /// <param name="buffer">The array source, or <see langword="null"/> for a snapshot range.</param>
        /// <param name="offset">The start within the selected source's coordinate system.</param>
        /// <param name="length">The number of source elements to enumerate, possibly zero.</param>
        /// <remarks>
        ///  Nonempty ranges have exactly one source. Buffer ranges bypass snapshot traversal entirely, and empty
        ///  ranges avoid piece lookup. Reusing this cursor preserves its allocated traversal-stack capacity.
        /// </remarks>
        internal void ResetTo(ImmutablePieceList<T>? snapshot, T[]? buffer, int offset, int length)
        {
            _root = new Piece(snapshot, buffer, offset, length, length);
            Reset();
        }

        /// <summary>
        ///  Enters a nonempty snapshot range and locates its first piece once.
        /// </summary>
        /// <param name="snapshot">The immutable source snapshot.</param>
        /// <param name="offset">A valid logical start within <paramref name="snapshot"/>.</param>
        /// <param name="length">The positive length of the selected source range.</param>
        /// <remarks>
        ///  The caller saves any unfinished parent continuation before entering a child. This method leaves
        ///  that stack untouched. Subsequent pieces are reached by advancing the stored index, not by searching
        ///  again for each element or descriptor.
        /// </remarks>
        private void SetSnapshotRange(ImmutablePieceList<T> snapshot, int offset, int length)
        {
            _snapshot = snapshot;
            _pieceIndex = FindPiece(snapshot._pieces, offset);
            _position = offset;
            _end = offset + length;
            _buffer = null;
            _bufferPosition = 0;
            _bufferEnd = 0;
        }
    }
}
