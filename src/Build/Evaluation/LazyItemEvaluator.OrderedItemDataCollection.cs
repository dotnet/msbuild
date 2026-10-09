// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    /// A collection of ItemData that maintains insertion order and internally optimizes some access patterns, e.g. bulk removal
    /// based on normalized item values.
    /// </summary>
    internal sealed partial class OrderedItemDataCollection
    {
        /// <summary>
        /// The list of items in the collection. Defines the enumeration order.
        /// </summary>
        private ImmutableList<ItemData> _list;

        private OrderedItemDataCollection(ImmutableList<ItemData> list)
        {
            _list = list;
        }

        /// <summary>
        /// Creates a new mutable collection.
        /// </summary>
        public static Builder CreateBuilder()
        {
            return new Builder(ImmutableList.CreateBuilder<ItemData>());
        }

        /// <summary>
        /// Creates a mutable view of this collection. Changes made to the returned builder are not reflected in this collection.
        /// </summary>
        public Builder ToBuilder()
        {
            return new Builder(_list.ToBuilder());
        }
    }
}
