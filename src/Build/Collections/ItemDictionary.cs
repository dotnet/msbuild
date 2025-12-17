// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
#if DEBUG
using Microsoft.Build.Shared;
#endif

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Collection of items that allows a list of all items of a specified type to be
    /// retrieved in O(1), and specific items to be added, removed, or checked for in O(1).
    /// All items of a particular type can also be removed in O(1).
    /// Items are ordered with respect to all other items of their type.
    /// </summary>
    /// <remarks>
    /// Really a Dictionary&lt;string, ICollection&lt;T&gt;&gt; where the key (the item type) is obtained from IKeyed.Key
    /// Is not observable, so if clients wish to observe modifications they must mediate them themselves and
    /// either not expose this collection or expose it through a readonly wrapper.
    /// At various places in this class locks are taken on the backing collection.  The reason for this is to allow
    /// this class to be asynchronously enumerated.  This is accomplished by the CopyOnReadEnumerable which will
    /// lock the backing collection when it does its deep cloning.  This prevents asynchronous access from corrupting
    /// the state of the enumeration until the collection has been fully copied.
    /// </remarks>
    /// <typeparam name="T">Item class type to store</typeparam>
    [DebuggerDisplay("#Item types={ItemTypes.Count} #Items={Count}")]
    internal sealed class ItemDictionary<T> : ICollection<T>, IItemDictionary<T>
        where T : class, IKeyed, IItem
    {
        /// <summary>
        /// Dictionary of item lists used as a backing store.
        /// This collection provides quick access to the ordered set of items of a particular type.
        /// </summary>
        private readonly Dictionary<string, List<T>> _itemLists;

        /// <summary>
        /// Constructor for an empty collection.
        /// </summary>
        public ItemDictionary()
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, List<T>>(MSBuildNameIgnoreCaseComparer.Default);
        }

        /// <summary>
        /// Constructor for an empty collection taking an initial capacity
        /// for the number of distinct item types
        /// </summary>
        public ItemDictionary(int initialItemTypesCapacity, int initialItemsCapacity = 0)
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, List<T>>(initialItemTypesCapacity, MSBuildNameIgnoreCaseComparer.Default);
        }

        /// <summary>
        /// Constructor for an collection holding items from a specified enumerable.
        /// </summary>
        public ItemDictionary(IEnumerable<T> items)
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, List<T>>(MSBuildNameIgnoreCaseComparer.Default);
            ImportItems(items);
        }

        /// <summary>
        /// Number of items in total, for debugging purposes.
        /// </summary>
        public int Count
        {
            get
            {
                int count = 0;
                lock (_itemLists)
                {
                    foreach (List<T> list in _itemLists.Values)
                    {
                        count += list.Count;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Get the item types that have at least one item in this collection
        /// </summary>
        /// <remarks>
        /// KeyCollection&lt;K&gt; is already a read only collection, so no protection
        /// is necessary.
        /// </remarks>
        public ICollection<string> ItemTypes
        {
            get
            {
                lock (_itemLists)
                {
                    return _itemLists.Keys;
                }
            }
        }

        public bool IsReadOnly => false;

        /// <summary>
        /// Returns the item list for a particular item type,
        /// creating and adding a new item list if necessary.
        /// Does not throw if there are no items of this type.
        /// This is a read-only list.
        /// If the result is not empty it is a live list.
        /// Use AddItem or RemoveItem to modify items in this project.
        /// Using the return value from this in a multithreaded situation is unsafe.
        /// </summary>
        public ICollection<T> this[string itemtype]
        {
            get
            {
                List<T> list;
                lock (_itemLists)
                {
                    if (!_itemLists.TryGetValue(itemtype, out list))
                    {
                        return Array.Empty<T>();
                    }
                }

                return new ReadOnlyCollection<T>(list);
            }
        }

        /// <summary>
        /// Empty the collection
        /// </summary>
        public void Clear()
        {
            lock (_itemLists)
            {
                foreach (List<T> list in _itemLists.Values)
                {
                    list.Clear();
                }

                _itemLists.Clear();
            }
        }

        /// <summary>
        /// Returns an enumerable which copies the underlying data on read.
        /// </summary>
        public IEnumerable<TResult> GetCopyOnReadEnumerable<TResult>(Func<T, TResult> selector)
        {
            return new CopyOnReadEnumerable<T, TResult>(this, _itemLists, selector);
        }

        /// <summary>
        /// Gets an enumerator over the items in the collection
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(_itemLists.Values);
        }

        /// <summary>
        /// Get an enumerator over entries
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _itemLists.GetEnumerator();
        }

        /// <summary>
        /// Enumerates item lists per each item type under the lock.
        /// </summary>
        public IEnumerable<(string itemType, IEnumerable<T> itemValue)> EnumerateItemsPerType()
        {
            lock (_itemLists)
            {
                foreach (var itemTypeBucket in _itemLists)
                {
                    if (itemTypeBucket.Value == null || itemTypeBucket.Value.Count == 0)
                    {
                        // skip empty markers
                        continue;
                    }

                    yield return (itemTypeBucket.Key, itemTypeBucket.Value);
                }
            }
        }

        /// <summary>
        /// Enumerates item lists per each item type under the lock.
        /// </summary>
        /// <param name="itemTypeCallback">
        /// A delegate that accepts the item type string and a list of items of that type.
        /// Will be called for each item type in the list.
        /// </param>
        public void EnumerateItemsPerType(Action<string, IEnumerable<T>> itemTypeCallback)
        {
            foreach (var tuple in EnumerateItemsPerType())
            {
                itemTypeCallback(tuple.itemType, tuple.itemValue);
            }
        }

        #region ItemDictionary<T> Members

        /// <summary>
        /// Returns all of the items for the specified type.
        /// If there are no items of this type, returns an empty list.
        /// Using the return from this method in a multithreaded scenario is unsafe.
        /// </summary>
        /// <param name="itemType">The item type to return</param>
        /// <returns>The list of matching items.</returns>
        public ICollection<T> GetItems(string itemType)
        {
            ICollection<T> result = this[itemType];

            return result ?? Array.Empty<T>();
        }

        #endregion

        /// <summary>
        /// Whether the provided item is in this table or not.
        /// </summary>
        bool ICollection<T>.Contains(T projectItem)
        {
            lock (_itemLists)
            {
                foreach (List<T> list in _itemLists.Values)
                {
                    if (list.Contains(projectItem))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Add a new item to the collection, at the
        /// end of the list of other items with its key.
        /// </summary>
        public void Add(T projectItem)
        {
            lock (_itemLists)
            {
                AddProjectItem(projectItem);
            }
        }

        /// <summary>
        /// Removes an item, if it is in the collection.
        /// Returns true if it was found, otherwise false.
        /// </summary>
        /// <remarks>
        /// If a list is emptied, removes the list from the enclosing collection
        /// so it can be garbage collected.
        /// </remarks>
        public bool Remove(T projectItem)
        {
            lock (_itemLists)
            {
                if (!_itemLists.TryGetValue(projectItem.Key, out List<T> list))
                {
                    return false;
                }

                // Searching for a single object - just compare the reference pointer.
                for (int i = 0; i < list.Count; i++)
                {
                    T candidateItem = list[i];
                    if (ReferenceEquals(candidateItem, projectItem))
                    {
                        list.RemoveAt(i);

                        // Save memory if the item type is now empty.
                        if (list.Count == 0)
                        {
                            _itemLists.Remove(projectItem.Key);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Add the set of items specified to this dictionary
        /// </summary>
        /// <param name="other">An enumerator over the items to remove.</param>
        public void ImportItems(IEnumerable<T> other)
        {
            lock (_itemLists)
            {
                foreach (var projectItem in other)
                {
                    AddProjectItem(projectItem);
                }
            }
        }

        /// <summary>
        /// Add the set of items specified, all sharing an item type, to this dictionary.
        /// </summary>
        /// <comment>
        /// This is a little faster than ImportItems where all the items have the same item type.
        /// </comment>
        public void ImportItemsOfType(string itemType, IEnumerable<T> items)
        {
            lock (_itemLists)
            {
                if (!_itemLists.TryGetValue(itemType, out List<T> list))
                {
                    list = new List<T>();
                    _itemLists[itemType] = list;
                }

                int count = list.Count;
                list.AddRange(items);

                for (int i = count; i < list.Count; i++)
                {
#if DEBUG
                    // Debug only: hot code path
                    ErrorUtilities.VerifyThrow(String.Equals(itemType, list[i].Key, StringComparison.OrdinalIgnoreCase), "Item type mismatch");
#endif
                }
            }
        }

        /// <summary>
        /// Remove the set of items specified from this dictionary
        /// </summary>
        /// <param name="itemType">The item type for all removes.</param>
        /// <param name="other">An enumerator over the items to remove.</param>
        public void RemoveItemsOfType(string itemType, IEnumerable<T> other)
        {
            lock (_itemLists)
            {
                if (!_itemLists.TryGetValue(itemType, out List<T> list))
                {
                    return;
                }

                // Since we'll need to search and remove an unknown number of items, we'll build up a new list of items to
                // keep, using the incoming enumerable as a set, and swap out the result at the end.
                // This minimizes the upper bound of ops and allocations here.
                List<T> listWithRemoves = new(list.Count);
                HashSet<T> itemsToRemove = new(other);
                foreach (T item in list)
                {
                    if (!itemsToRemove.Contains(item))
                    {
                        listWithRemoves.Add(item);
                    }
                }

                if (listWithRemoves.Count > 0)
                {
                    _itemLists[itemType] = listWithRemoves;
                }
                else
                {
                    // If the clone is empty, remove the item type from the dictionary
                    _itemLists.Remove(itemType);
                }
            }
        }

        private void AddProjectItem(T projectItem)
        {
            if (!_itemLists.TryGetValue(projectItem.Key, out List<T> list))
            {
                list = new List<T>();
                _itemLists[projectItem.Key] = list;
            }

            list.Add(projectItem);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (Count > array.Length - arrayIndex)
            {
                throw new ArgumentException(nameof(array));
            }

            foreach (T item in this)
            {
                array[arrayIndex] = item;
                ++arrayIndex;
            }
        }

        /// <summary>
        /// Custom enumerator that allows enumeration over all the items in the collection
        /// as though they were in a single list.
        /// All items of a type are returned consecutively in their correct order.
        /// However the order in which item types are returned is not defined.
        /// </summary>
        private sealed class Enumerator : IEnumerator<T>
        {
            /// <summary>
            /// Enumerator over lists
            /// </summary>
            private IEnumerator<ICollection<T>> _listEnumerator;

            /// <summary>
            /// Enumerator over items in the current list
            /// </summary>
            private IEnumerator<T> _itemEnumerator;

            /// <summary>
            /// Constructs an item enumerator over a list enumerator
            /// </summary>
            internal Enumerator(IEnumerable<ICollection<T>> listEnumerable)
            {
                _listEnumerator = listEnumerable.GetEnumerator(); // Now get the enumerator, since we now have the lock.
                _itemEnumerator = null; // Must assign all struct fields first
                _itemEnumerator = GetNextItemEnumerator();
            }

            /// <summary>
            /// Get the current item
            /// </summary>
            /// <remarks>Undefined if enumerator is before or after collection: we return null.</remarks>
            public T Current => _itemEnumerator?.Current;

            /// <summary>
            /// Implementation of IEnumerator.Current, which unlike IEnumerator&gt;T&lt;.Current throws
            /// if there is no current object
            /// </summary>
            // will throw InvalidOperationException, per IEnumerator contract
            object IEnumerator.Current => _itemEnumerator != null ? _itemEnumerator.Current : ((IEnumerator)_listEnumerator).Current;

            /// <summary>
            /// Move to the next object if any,
            /// otherwise returns false
            /// </summary>
            public bool MoveNext()
            {
                if (_itemEnumerator == null)
                {
                    return false;
                }

                while (!_itemEnumerator.MoveNext())
                {
                    _itemEnumerator = GetNextItemEnumerator();

                    if (_itemEnumerator == null)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Reset the enumerator
            /// </summary>
            public void Reset()
            {
                _itemEnumerator?.Reset();
                _listEnumerator.Reset();
            }

            /// <summary>
            /// IDisposable implementation.
            /// </summary>
            public void Dispose()
            {
                if (_listEnumerator != null)
                {
                    if (_itemEnumerator != null)
                    {
                        _itemEnumerator.Dispose();
                        _itemEnumerator = null;
                    }

                    _listEnumerator.Dispose();
                    _listEnumerator = null;
                }
            }

            /// <summary>
            /// Get an item enumerator over the next list with items in it
            /// </summary>
            private IEnumerator<T> GetNextItemEnumerator()
            {
                do
                {
                    if (!_listEnumerator.MoveNext())
                    {
                        return null;
                    }
                }
                while (_listEnumerator.Current == null || _listEnumerator.Current.Count == 0);

                return _listEnumerator.Current.GetEnumerator();
            }
        }
    }
}
