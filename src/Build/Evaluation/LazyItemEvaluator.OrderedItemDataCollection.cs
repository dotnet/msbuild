// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    /// A collection of ItemData that maintains insertion order and internally optimizes some access patterns, e.g. bulk removal
    /// based on normalized item values.
    /// </summary>
    private sealed partial class OrderedItemDataCollection : IEnumerable<ItemData>
    {
        /// <summary>
        /// The list of items in the collection. Defines the enumeration order.
        /// </summary>
        private readonly ImmutableList<ItemData> _list;

        /// <summary>
        ///  Gets the shared empty item collection.
        /// </summary>
        public static OrderedItemDataCollection Empty { get; } = new([]);

        private OrderedItemDataCollection(ImmutableList<ItemData> list)
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
            => new(ImmutableList.CreateBuilder<ItemData>());

        /// <summary>
        /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
        /// </summary>
        public Builder ToBuilder()
            => new(_list.ToBuilder());

        /// <summary>
        ///  Gets an enumerator over the items in insertion order.
        /// </summary>
        /// <returns>
        ///  An enumerator over this snapshot.
        /// </returns>
        public ImmutableList<ItemData>.Enumerator GetEnumerator() => _list.GetEnumerator();

        IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator() => GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
