// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  An immutable snapshot of computed item data, sharing unchanged ranges with earlier snapshots.
    /// </summary>
    private sealed partial class OrderedItemDataCollection : IEnumerable<ItemData>
    {
        /// <summary>
        ///  The piece-list snapshot that defines item order and preserves historical item states.
        /// </summary>
        private readonly ImmutablePieceList<ItemData> _list;

        private ICollection<I> _filteredItems;

        /// <summary>
        ///  Gets the shared empty item collection.
        /// </summary>
        public static OrderedItemDataCollection Empty { get; } = new(ImmutablePieceList<ItemData>.Empty);

        private OrderedItemDataCollection(ImmutablePieceList<ItemData> list)
        {
            _list = list;
        }

        /// <summary>
        ///  Gets the number of items in this snapshot.
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// Creates a new mutable collection.
        /// </summary>
        public static Builder CreateBuilder()
            => new(ImmutablePieceList<ItemData>.Empty.ToEditor());

        /// <summary>
        /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
        /// </summary>
        public Builder ToBuilder()
            => new(_list.ToEditor());

        /// <summary>
        ///  Gets items filtered by originating Include conditions, caching the collection on this snapshot.
        /// </summary>
        /// <returns>
        ///  The same read-only collection of filtered items in evaluation order on each call.
        /// </returns>
        public ICollection<I> GetFilteredItems()
        {
            if (_filteredItems is not null)
            {
                return _filteredItems;
            }

            if (Count == 0)
            {
                return _filteredItems = ImmutableArray<I>.Empty;
            }

            using RefArrayBuilder<I> items = new(Count);
            foreach (ItemData data in this)
            {
                if (data.ConditionResult)
                {
                    items.Add(data.Item);
                }
            }

            return _filteredItems = items.ToImmutable();
        }

        /// <summary>
        ///  Gets an enumerator over the items in insertion order.
        /// </summary>
        /// <returns>
        ///  An enumerator over this snapshot.
        /// </returns>
        public ImmutablePieceList<ItemData>.Enumerator GetEnumerator() => _list.GetEnumerator();

        IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
