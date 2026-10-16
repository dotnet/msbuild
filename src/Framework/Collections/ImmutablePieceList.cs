// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Collections;

/// <summary>
///  An immutable sequence described by ranges of earlier snapshots and newly owned item storage.
/// </summary>
/// <typeparam name="T">The type of elements in the sequence.</typeparam>
/// <remarks>
///  An editor starts with a single range covering its base snapshot, without copying that snapshot's piece index.
///  Edits split that range and introduce new storage. creating a snapshot freezes this local description and makes it
///  the base for the next delta. Indexing follows snapshot references iteratively, using binary search within
///  each visited piece index. Enumeration uses an explicit traversal stack rather than recursion.
///  <para>
///   Snapshot creation examines only the delta's pieces to calculate its depth; it does not copy items or inherited
///   descriptors. Replacement and range removal can shift the delta's descriptors. Long snapshot ancestry can increase
///   lookup depth, and a reference to a range retains its entire source snapshot and its ancestors.
///  </para>
///  <para>
///   Immutability is shallow: objects referenced by elements must be immutable or cloned by the caller before mutation.
///   Buffer ranges also retain their entire arrays, including elements removed from the logical sequence.
///  </para>
/// </remarks>
internal sealed partial class ImmutablePieceList<T> : IReadOnlyList<T>
{
    /// <summary>
    ///  The frozen local piece index, ordered by strictly increasing exclusive logical ends.
    /// </summary>
    /// <remarks>
    ///  Ownership is transferred from an editor on snapshot creation. Despite its mutable type, this list is never
    ///  changed after becoming part of a snapshot. The final piece ends at <see cref="Count"/>.
    /// </remarks>
    private readonly List<Piece> _pieces;

    /// <summary>
    ///  Takes ownership of a completed delta and records its logical size and source-graph depth.
    /// </summary>
    /// <param name="pieces">The local piece index, which the caller must no longer modify.</param>
    /// <param name="count">The sum of the piece lengths, or zero for an empty index.</param>
    /// <remarks>
    ///  No item storage or ancestor piece indices are copied. Cached parent depths avoid traversing snapshot ancestry
    ///  while creating a snapshot a new snapshot.
    /// </remarks>
    private ImmutablePieceList(List<Piece> pieces, int count)
    {
        _pieces = pieces;
        Count = count;
        foreach (Piece piece in pieces)
        {
            if (piece.Snapshot is { } source)
            {
                Depth = Math.Max(Depth, checked(source.Depth + 1));
            }
        }
    }

    /// <summary>
    ///  Gets the shared empty snapshot.
    /// </summary>
    public static ImmutablePieceList<T> Empty { get; } = new([], 0);

    /// <summary>
    ///  Gets the number of elements in this snapshot.
    /// </summary>
    public int Count { get; }

    /// <summary>
    ///  Gets the number of ranges in this snapshot's local piece index, without expanding ancestor ranges.
    /// </summary>
    public int PieceCount => _pieces.Count;

    /// <summary>
    ///  Gets the maximum number of ancestor links in this snapshot's source graph.
    /// </summary>
    /// <remarks>
    ///  Buffer-only and empty snapshots have depth zero. A referenced snapshot contributes its entire graph
    ///  depth, even if the selected range does not visit that snapshot's deepest pieces. This is therefore
    ///  a structural upper bound, not the number of links followed for every item lookup.
    /// </remarks>
    public int Depth { get; }

    /// <summary>
    ///  Gets the element at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based logical position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the sequence.</exception>
    public T this[int index] => ItemRef(index);

    /// <summary>
    ///  Gets a read-only reference to the element at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based logical position.</param>
    /// <returns>
    ///  A reference to the element in its shared storage.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the sequence.</exception>
    /// <remarks>
    ///  The external position is validated here. Ancestor positions are derived from immutable, valid source
    ///  ranges and do not repeat public argument validation at each level.
    /// </remarks>
    public ref readonly T ItemRef(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        Piece piece = _pieces[FindPiece(_pieces, index)];
        return ref GetItemRef(piece, index - (piece.End - piece.Length));
    }

    /// <summary>
    ///  Creates an independent editor based on a single range of this snapshot.
    /// </summary>
    /// <returns>
    ///  An editor whose changes cannot modify this snapshot or other editors.
    /// </returns>
    public Editor ToEditor() => new(this);

    /// <summary>
    ///  Gets an enumerator that traverses snapshot ranges iteratively without searching for each element.
    /// </summary>
    /// <returns>
    ///  An enumerator over the snapshot in logical order.
    /// </returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    ///  Resolves a position within a piece to its final buffer cell without recursive snapshot calls.
    /// </summary>
    /// <param name="piece">A nonempty piece with a valid snapshot or buffer source.</param>
    /// <param name="offset">The position relative to the beginning of <paramref name="piece"/>.</param>
    /// <returns>
    ///  A read-only reference to the cell in the owned buffer reached through the source ranges.
    /// </returns>
    /// <remarks>
    ///  Callers supply a valid piece-relative offset. Each step translates that offset into the ancestor's
    ///  logical coordinate system, then selects the containing piece. snapshot ancestry depth consumes no call-stack space.
    /// </remarks>
    private static ref readonly T GetItemRef(Piece piece, int offset)
    {
        while (piece.Snapshot is { } source)
        {
            // Offset addresses the source; End describes the enclosing sequence, not that source.
            int index = piece.Offset + offset;
            piece = source._pieces[FindPiece(source._pieces, index)];
            offset = index - (piece.End - piece.Length);
        }

        return ref piece.Buffer![piece.Offset + offset];
    }

    /// <summary>
    ///  Finds the piece containing a trusted logical position in a nonempty local index.
    /// </summary>
    /// <param name="pieces">The piece index, sorted by exclusive logical end.</param>
    /// <param name="index">A validated position within the logical sequence described by <paramref name="pieces"/>.</param>
    /// <returns>
    ///  The index of the first piece whose exclusive end exceeds <paramref name="index"/>.
    /// </returns>
    /// <remarks>
    ///  Validation belongs to public entry points; internal source-range traversal maintains the same bounds
    ///  invariant. Testing the first piece directly also handles a single-piece index without binary search.
    /// </remarks>
    private static int FindPiece(List<Piece> pieces, int index)
    {
        Debug.Assert(pieces.Count > 0 && index >= 0 && index < pieces[pieces.Count - 1].End);

        if (index < pieces[0].End)
        {
            return 0;
        }

        // End is exclusive, so find the first piece ending at or beyond index + 1.
        Piece searchKey = new(null, [], 0, 0, index + 1);
        int result = pieces.BinarySearch(searchKey);
        // An exact end matches its piece; otherwise the insertion point identifies the next larger end.
        return result >= 0 ? result : ~result;
    }
}
