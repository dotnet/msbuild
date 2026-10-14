// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Provides read-only lookup of the item lists captured by an operation.
    /// </summary>
    private abstract partial class ReferencedItemLists : IItemProvider<I>
    {
        private const int SmallMapLimit = 4;

        /// <summary>
        ///  Associates an item type with the list captured at an operation's declaration.
        /// </summary>
        /// <param name="ItemType">The referenced item type.</param>
        /// <param name="ItemList">The captured item list.</param>
        private readonly record struct Entry(string ItemType, LazyItemList ItemList);

        /// <summary>
        ///  Gets the shared empty mapping.
        /// </summary>
        public static ReferencedItemLists Empty { get; } = new EmptyItemLists();

        private ReferencedItemLists()
        {
        }

        /// <summary>
        ///  Gets the number of distinct referenced item types.
        /// </summary>
        public abstract int Count { get; }

        /// <inheritdoc />
        ICollection<I> IItemProvider<I>.GetItems(string itemType)
            => TryGetValue(itemType, out LazyItemList? items)
                ? items.GetMatchedItems(ImmutableHashSet<string>.Empty)
                : Array.Empty<I>();

        /// <summary>
        ///  Creates an independently owned mapping, replacing earlier references to the same item type.
        /// </summary>
        /// <param name="references">The captured item types and lists, in capture order.</param>
        /// <returns>
        ///  A mapping using empty, singleton, bounded small-map, or dictionary storage.
        /// </returns>
        private static ReferencedItemLists Create(ReadOnlySpan<Entry> references)
        {
            if (references.IsEmpty)
            {
                return Empty;
            }

            StringComparer comparer = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;

            if (references.Length == 1)
            {
                return new SingleItemList(references[0], comparer);
            }

            using RefArrayBuilder<Entry> entries = default;
            entries.Add(references[0]);
            Dictionary<string, LazyItemList>? dictionary = null;

            for (int i = 1; i < references.Length; i++)
            {
                Entry reference = references[i];

                if (dictionary is not null)
                {
                    dictionary[reference.ItemType] = reference.ItemList;
                    continue;
                }

                int existingIndex = -1;
                for (int j = 0; j < entries.Count; j++)
                {
                    if (comparer.Equals(entries[j].ItemType, reference.ItemType))
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    entries.AsSpan()[existingIndex] = new(entries[existingIndex].ItemType, reference.ItemList);
                }
                else if (entries.Count < SmallMapLimit)
                {
                    entries.Add(reference);
                }
                else
                {
                    dictionary = new Dictionary<string, LazyItemList>(SmallMapLimit + 1, comparer);
                    for (int j = 0; j < entries.Count; j++)
                    {
                        dictionary.Add(entries[j].ItemType, entries[j].ItemList);
                    }

                    dictionary.Add(reference.ItemType, reference.ItemList);
                }
            }

            if (dictionary is not null)
            {
                return new DictionaryItemLists(dictionary);
            }

            return entries.Count == 1
                ? new SingleItemList(entries[0], comparer)
                : new SmallItemLists(entries.ToImmutable(), comparer);
        }

        /// <summary>
        ///  Looks up the captured list for an item type.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="itemList">The captured list, or <see langword="null"/> when the item type is absent.</param>
        /// <returns>
        ///  <see langword="true"/> when the item type is present; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetValue(string itemType, [NotNullWhen(true)] out LazyItemList? itemList)
        {
            ArgumentNullException.ThrowIfNull(itemType);
            return TryGetValueCore(itemType, out itemList);
        }

        protected abstract bool TryGetValueCore(string itemType, [NotNullWhen(true)] out LazyItemList? itemList);

        private sealed class EmptyItemLists : ReferencedItemLists
        {
            public override int Count => 0;

            protected override bool TryGetValueCore(string itemType, [NotNullWhen(true)] out LazyItemList? itemList)
            {
                itemList = null;
                return false;
            }
        }

        private sealed class SingleItemList(Entry entry, StringComparer comparer) : ReferencedItemLists
        {
            public override int Count => 1;

            protected override bool TryGetValueCore(string itemType, [NotNullWhen(true)] out LazyItemList? itemList)
            {
                if (comparer.Equals(entry.ItemType, itemType))
                {
                    itemList = entry.ItemList;
                    return true;
                }

                itemList = null;
                return false;
            }
        }

        private sealed class SmallItemLists(ImmutableArray<Entry> entries, StringComparer comparer) : ReferencedItemLists
        {
            public override int Count => entries.Length;

            protected override bool TryGetValueCore(string itemType, [NotNullWhen(true)] out LazyItemList? itemList)
            {
                foreach (Entry entry in entries)
                {
                    if (comparer.Equals(entry.ItemType, itemType))
                    {
                        itemList = entry.ItemList;
                        return true;
                    }
                }

                itemList = null;
                return false;
            }
        }

        private sealed class DictionaryItemLists(Dictionary<string, LazyItemList> entries) : ReferencedItemLists
        {
            public override int Count => entries.Count;

            protected override bool TryGetValueCore(string itemType, [NotNullWhen(true)] out LazyItemList? itemList)
                => entries.TryGetValue(itemType, out itemList);
        }
    }
}
