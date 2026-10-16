// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Collections;

internal sealed partial class ImmutablePieceList<T>
{
    /// <summary>
    ///  Describes a source range and its position in an enclosing sequence's local piece index.
    /// </summary>
    /// <param name="Snapshot">The source snapshot, or <see langword="null"/> when <paramref name="Buffer"/> supplies the elements.</param>
    /// <param name="Buffer">The owned array source, or <see langword="null"/> for a snapshot range.</param>
    /// <param name="Offset">The start of the range in its source's coordinate system.</param>
    /// <param name="Length">The number of source elements included in the range.</param>
    /// <param name="End">The exclusive logical end in the enclosing sequence, not in the source.</param>
    /// <remarks>
    ///  Stored pieces are nonempty and have exactly one source. Their logical start is <c>End - Length</c>,
    ///  which need not equal <paramref name="Offset"/>. Splitting a piece preserves its source identity while
    ///  adjusting the source range and logical end independently.
    ///  <para>
    ///   Zero-length pieces are also used temporarily for slicing, binary-search keys, and empty cursor state;
    ///   they are not appended to a snapshot's piece index.
    ///  </para>
    /// </remarks>
    private readonly record struct Piece(
        ImmutablePieceList<T>? Snapshot,
        T[]? Buffer,
        int Offset,
        int Length,
        int End) : IComparable<Piece>
    {
        /// <summary>
        ///  Describes a range of an already immutable snapshot without expanding its pieces.
        /// </summary>
        /// <param name="snapshot">The immutable source snapshot.</param>
        /// <param name="offset">The logical start within <paramref name="snapshot"/>.</param>
        /// <param name="length">The number of source elements in the range.</param>
        /// <returns>
        ///  A piece with a provisional logical end equal to <paramref name="length"/>.
        /// </returns>
        /// <remarks>
        ///  Callers supply a valid source range and adjust the logical end when placing the piece in an index.
        /// </remarks>
        public static Piece FromSnapshot(ImmutablePieceList<T> snapshot, int offset, int length)
            => new(snapshot, null, offset, length, length);

        /// <summary>
        ///  Describes a range of owned array storage whose referenced cells will not be overwritten.
        /// </summary>
        /// <param name="buffer">The owned array containing the source elements.</param>
        /// <param name="offset">The first array index in the range.</param>
        /// <param name="length">The number of array elements in the range.</param>
        /// <returns>
        ///  A piece with a provisional logical end equal to <paramref name="length"/>.
        /// </returns>
        /// <remarks>
        ///  No elements are copied. Only the editor that owns the buffer may continue writing its unused tail.
        /// </remarks>
        public static Piece FromBuffer(T[] buffer, int offset, int length)
            => new(null, buffer, offset, length, length);

        /// <summary>
        ///  Determines whether two ranges refer to the same snapshot or array object.
        /// </summary>
        /// <param name="other">The piece whose source identity is compared.</param>
        /// <returns>
        ///  <see langword="true"/> for identical source objects, regardless of range bounds or logical positions.
        /// </returns>
        public bool HasSameSource(Piece other)
            => ReferenceEquals(Snapshot, other.Snapshot) && ReferenceEquals(Buffer, other.Buffer);

        /// <summary>
        ///  Compares exclusive logical ends for ordered piece lookup.
        /// </summary>
        /// <param name="other">The indexed piece or search key to compare.</param>
        /// <returns>
        ///  A negative value, zero, or a positive value when this end precedes, equals, or follows the other end.
        /// </returns>
        /// <remarks>
        ///  Source identity and source-range bounds do not participate in ordering. Comparison by end alone is
        ///  intentional and is distinct from the record's full value equality.
        /// </remarks>
        public int CompareTo(Piece other) => End.CompareTo(other.End);
    }
}
