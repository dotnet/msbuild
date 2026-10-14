// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A simple implementation of ItemDictionary intended for populating Scope tables and merging adds/removes,
    /// without the overhead of locking and additional lookups.
    /// </summary>
    internal sealed class ItemDictionarySlim : IEnumerable<KeyValuePair<string, List<ProjectItemInstance>>>
    {
        private readonly Dictionary<string, List<ProjectItemInstance>> _itemLists;

        public ItemDictionarySlim() =>
            _itemLists = new Dictionary<string, List<ProjectItemInstance>>(MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// Gets all items of the given type, or null if there are none.
        /// </summary>
        public List<ProjectItemInstance>? this[string itemType] =>
            _itemLists.TryGetValue(itemType, out List<ProjectItemInstance>? itemsOfType) ? itemsOfType : null;

        /// <summary>
        /// Returns true if there are any items of the given type.
        /// </summary>
        public bool ContainsKey(string itemType) => _itemLists.ContainsKey(itemType);

        /// <summary>
        /// Adds a single item to the matching list for its type.
        /// </summary>
        public void Add(ProjectItemInstance projectItem)
        {
            if (!_itemLists.TryGetValue(projectItem.ItemType, out List<ProjectItemInstance>? list))
            {
                list = [];
                _itemLists[projectItem.ItemType] = list;
            }

            list.Add(projectItem);
        }

        /// <summary>
        /// Imports and merges all items from an existing item dictionary, appending to any existing list.
        /// </summary>
        /// <remarks>
        /// This should only be called when merging between scopes, as it may take list references from the other dictionary.
        /// This is safe since we create these internal lists, and the owning Scope will always be discarded after a merge.
        /// </remarks>
        public void ImportItems(ItemDictionarySlim other)
        {
            foreach (KeyValuePair<string, List<ProjectItemInstance>> kvp in other._itemLists)
            {
                string itemType = kvp.Key;
                List<ProjectItemInstance> itemsToAdd = kvp.Value;

                if (_itemLists.TryGetValue(itemType, out List<ProjectItemInstance>? list))
                {
                    // There are already items of this type, so append to the existing list
                    list.AddRange(itemsToAdd);
                }
                else
                {
                    // Otherwise, steal the reference instead of copying items out.
                    _itemLists[itemType] = itemsToAdd;
                }
            }
        }

        /// <summary>
        /// Imports and merges all items of the given type, appending to any existing list.
        /// </summary>
        public void ImportItemsOfType(string itemType, IEnumerable<ProjectItemInstance> items)
        {
            if (!_itemLists.TryGetValue(itemType, out List<ProjectItemInstance>? list))
            {
                list = [];
                _itemLists[itemType] = list;
            }

            list.AddRange(items);
        }

        /// <summary>
        /// Gets the item list for the given type, creating it if needed, with room for at least
        /// <paramref name="additionalCapacity"/> more items than it currently holds.
        /// </summary>
        internal List<ProjectItemInstance> GetOrCreateListWithRoomFor(string itemType, int additionalCapacity)
        {
            if (!_itemLists.TryGetValue(itemType, out List<ProjectItemInstance>? list))
            {
                list = new List<ProjectItemInstance>(additionalCapacity);
                _itemLists[itemType] = list;
            }
            else
            {
                int required = list.Count + additionalCapacity;
                if (required > list.Capacity)
                {
                    // Grow geometrically, as List<T>.Add would; an exact fit would reallocate on
                    // every call when many small batches append to the same list.
                    list.Capacity = Math.Max(required, list.Capacity * 2);
                }
            }

            return list;
        }

        /// <summary>
        /// Sets the capacity for the item list matching the given type.
        /// </summary>
        /// <remarks>
        /// Useful for dealing with IEnumerable if we know the upper bound or estimate of items to be added.
        /// </remarks>
        internal void EnsureCapacityForItemType(string itemType, int capacity)
        {
            if (!_itemLists.TryGetValue(itemType, out List<ProjectItemInstance>? list))
            {
                list = new List<ProjectItemInstance>(capacity);
                _itemLists[itemType] = list;
            }
            else if (capacity > list.Capacity)
            {
                // Conditional since List.Capacity will throw if set less than its current value.
                list.Capacity = capacity;
            }
        }

        public Dictionary<string, List<ProjectItemInstance>>.Enumerator GetEnumerator() => _itemLists.GetEnumerator();

        IEnumerator<KeyValuePair<string, List<ProjectItemInstance>>> IEnumerable<KeyValuePair<string, List<ProjectItemInstance>>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
