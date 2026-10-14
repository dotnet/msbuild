// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Provides items through the bookmarks established when an operation was collected.
    /// </summary>
    private abstract partial class ItemListBookmarks : IItemProvider<I>
    {
        private const int SmallMapLimit = 4;

        /// <summary>
        ///  Associates an item type with its bookmark at an operation's declaration.
        /// </summary>
        /// <param name="ItemType">The referenced item type.</param>
        /// <param name="ItemList">The bookmark for this item type.</param>
        private readonly record struct Entry(string ItemType, ItemListBookmark ItemList);

        /// <summary>
        ///  Gets the shared empty mapping.
        /// </summary>
        public static ItemListBookmarks Empty { get; } = new EmptyItemLists();

        private ItemListBookmarks()
        {
        }

        /// <summary>
        ///  Gets the number of distinct referenced item types.
        /// </summary>
        public abstract int Count { get; }

        /// <inheritdoc />
        ICollection<I> IItemProvider<I>.GetItems(string itemType)
            => TryGetValue(itemType, out ItemListBookmark items)
                ? items.GetFilteredItems()
                : Array.Empty<I>();

        /// <summary>
        ///  Creates an independently owned mapping, replacing earlier references to the same item type.
        /// </summary>
        /// <param name="references">The item types and bookmarks, in collection order.</param>
        /// <returns>
        ///  A mapping using empty, singleton, bounded small-map, or dictionary storage.
        /// </returns>
        private static ItemListBookmarks Create(ReadOnlySpan<Entry> references)
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
            Dictionary<string, ItemListBookmark>? dictionary = null;

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
                    dictionary = new Dictionary<string, ItemListBookmark>(SmallMapLimit + 1, comparer);
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
        ///  Looks up the bookmark for an item type.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="itemList">The bookmark, or the default value when the item type is absent.</param>
        /// <returns>
        ///  <see langword="true"/> when the item type is present; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetValue(string itemType, out ItemListBookmark itemList)
        {
            ArgumentNullException.ThrowIfNull(itemType);
            return TryGetValueCore(itemType, out itemList);
        }

        protected abstract bool TryGetValueCore(string itemType, out ItemListBookmark itemList);

        private sealed class EmptyItemLists : ItemListBookmarks
        {
            public override int Count => 0;

            protected override bool TryGetValueCore(string itemType, out ItemListBookmark itemList)
            {
                itemList = default;
                return false;
            }
        }

        private sealed class SingleItemList(Entry entry, StringComparer comparer) : ItemListBookmarks
        {
            public override int Count => 1;

            protected override bool TryGetValueCore(string itemType, out ItemListBookmark itemList)
            {
                if (comparer.Equals(entry.ItemType, itemType))
                {
                    itemList = entry.ItemList;
                    return true;
                }

                itemList = default;
                return false;
            }
        }

        private sealed class SmallItemLists(ImmutableArray<Entry> entries, StringComparer comparer) : ItemListBookmarks
        {
            public override int Count => entries.Length;

            protected override bool TryGetValueCore(string itemType, out ItemListBookmark itemList)
            {
                foreach (Entry entry in entries)
                {
                    if (comparer.Equals(entry.ItemType, itemType))
                    {
                        itemList = entry.ItemList;
                        return true;
                    }
                }

                itemList = default;
                return false;
            }
        }

        private sealed class DictionaryItemLists(Dictionary<string, ItemListBookmark> entries) : ItemListBookmarks
        {
            public override int Count => entries.Count;

            protected override bool TryGetValueCore(string itemType, out ItemListBookmark itemList)
                => entries.TryGetValue(itemType, out itemList);
        }
    }
}
