// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

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
    internal sealed class ItemDictionary<T> : IEnumerable<T>, IItemProvider<T>
        where T : class, IKeyed, IItem
    {
        /// <summary>
        /// Dictionary of item lists used as a backing store.
        /// An empty list should never be stored in here unless it is an empty marker.
        /// See <see cref="AddEmptyMarker">AddEmptyMarker</see>.
        /// This collection provides quick access to the ordered set of items of a particular type.
        /// </summary>
        private readonly Dictionary<string, LinkedList<T>> _itemLists;

        /// <summary>
        /// Dictionary of items in the collection, to speed up Contains,
        /// Remove, and Replace. For those operations, we look up here,
        /// then modify the other dictionary to match.
        /// </summary>
        private readonly Dictionary<T, LinkedListNode<T>> _nodes;

        /// <summary>
        /// Constructor for an empty collection.
        /// </summary>
        internal ItemDictionary()
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, LinkedList<T>>(MSBuildNameIgnoreCaseComparer.Default);
            _nodes = new Dictionary<T, LinkedListNode<T>>();
        }

        /// <summary>
        /// Constructor for an empty collection taking an initial capacity
        /// for the number of distinct item types
        /// </summary>
        internal ItemDictionary(int initialItemTypesCapacity, int initialItemsCapacity = 0)
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, LinkedList<T>>(initialItemTypesCapacity, MSBuildNameIgnoreCaseComparer.Default);
            _nodes = new Dictionary<T, LinkedListNode<T>>(initialItemsCapacity);
        }

        /// <summary>
        /// Constructor for an collection holding items from a specified enumerable.
        /// </summary>
        internal ItemDictionary(IEnumerable<T> items)
        {
            // Tracing.Record("new item dictionary");
            _itemLists = new Dictionary<string, LinkedList<T>>(MSBuildNameIgnoreCaseComparer.Default);
            _nodes = new Dictionary<T, LinkedListNode<T>>();
            ImportItems(items);
        }

        /// <summary>
        /// Number of items in total, for debugging purposes.
        /// </summary>
        internal int Count => _nodes.Count;

        /// <summary>
        /// Get the item types that have at least one item in this collection
        /// </summary>
        /// <remarks>
        /// KeyCollection&lt;K&gt; is already a read only collection, so no protection
        /// is necessary.
        /// </remarks>
        internal ICollection<string> ItemTypes
        {
            get
            {
                lock (_itemLists)
                {
                    return _itemLists.Keys;
                }
            }
        }

        /// <summary>
        /// Returns the item list for a particular item type,
        /// creating and adding a new item list if necessary.
        /// Does not throw if there are no items of this type.
        /// This is a read-only list.
        /// If the result is not empty it is a live list.
        /// Use AddItem or RemoveItem to modify items in this project.
        /// Using the return value from this in a multithreaded situation is unsafe.
        /// </summary>
        internal ICollection<T> this[string itemtype]
        {
            get
            {
                LinkedList<T> list;
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
                foreach (ICollection<T> list in _itemLists.Values)
                {
                    list.Clear();
                }

                _itemLists.Clear();
                _nodes.Clear();
            }
        }

        /// <summary>
        /// Returns an enumerable which copies the underlying data on read.
        /// </summary>
        public IEnumerable<T> GetCopyOnReadEnumerable()
        {
            return new CopyOnReadEnumerable<T>(this, _itemLists);
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
        internal bool Contains(T projectItem)
        {
            lock (_itemLists)
            {
                return _nodes.ContainsKey(projectItem);
            }
        }

        /// <summary>
        /// Add a new item to the collection, at the
        /// end of the list of other items with its key.
        /// </summary>
        internal void Add(T projectItem)
        {
            lock (_itemLists)
            {
                if (!_itemLists.TryGetValue(projectItem.Key, out LinkedList<T> list))
                {
                    list = new LinkedList<T>();
                    _itemLists[projectItem.Key] = list;
                }

                LinkedListNode<T> node = list.AddLast(projectItem);
                _nodes.Add(projectItem, node);
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
        internal bool Remove(T projectItem)
        {
            lock (_itemLists)
            {
                if (!_nodes.TryGetValue(projectItem, out LinkedListNode<T> node))
                {
                    return false;
                }

                LinkedList<T> list = node.List;
                list.Remove(node);
                _nodes.Remove(projectItem);

                // Save memory
                if (list.Count == 0)
                {
                    _itemLists.Remove(projectItem.Key);
                }

                return true;
            }
        }

        /// <summary>
        /// Replaces an exsting item with a new item.  This is necessary to preserve the original ordering semantics of Lookup.GetItems
        /// when items with metadata modifications are being returned.  See Dev10 bug 480737.
        /// If the item is not found, does nothing.
        /// </summary>
        /// <param name="existingItem">The item to be replaced.</param>
        /// <param name="newItem">The replacement item.</param>
        internal void Replace(T existingItem, T newItem)
        {
            ErrorUtilities.VerifyThrow(existingItem.Key == newItem.Key, "Cannot replace an item {0} with an item {1} with a different name.", existingItem.Key, newItem.Key);
            lock (_itemLists)
            {
                if (_nodes.TryGetValue(existingItem, out LinkedListNode<T> node))
                {
                    node.Value = newItem;
                    _nodes.Remove(existingItem);
                    _nodes.Add(newItem, node);
                }
            }
        }

        /// <summary>
        /// Add the set of items specified to this dictionary
        /// </summary>
        /// <param name="other">An enumerator over the items to remove.</param>
        internal void ImportItems(IEnumerable<T> other)
        {
            foreach (T item in other)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Add the set of items specified, all sharing an item type, to this dictionary.
        /// </summary>
        /// <comment>
        /// This is a little faster than ImportItems where all the items have the same item type.
        /// </comment>
        internal void ImportItemsOfType(string itemType, IEnumerable<T> items)
        {
            lock (_itemLists)
            {
                if (!_itemLists.TryGetValue(itemType, out LinkedList<T> list))
                {
                    list = new LinkedList<T>();
                    _itemLists[itemType] = list;
                }

                foreach (T item in items)
                {
#if DEBUG
                    // Debug only: hot code path
                    ErrorUtilities.VerifyThrow(String.Equals(itemType, item.Key, StringComparison.OrdinalIgnoreCase), "Item type mismatch");
#endif
                    LinkedListNode<T> node = list.AddLast(item);
                    _nodes.Add(item, node);
                }
            }
        }

        /// <summary>
        /// Remove the set of items specified from this dictionary
        /// </summary>
        /// <param name="other">An enumerator over the items to remove.</param>
        internal void RemoveItems(IEnumerable<T> other)
        {
            foreach (T item in other)
            {
                Remove(item);
            }
        }

        /// <summary>
        /// Special method used for batching buckets.
        /// Adds an explicit marker indicating there are no items for the specified item type.
        /// In the general case, this is redundant, but batching buckets use this to indicate that they are
        /// batching over the item type, but their bucket does not contain items of that type.
        /// See <see cref="HasEmptyMarker">HasEmptyMarker</see>.
        /// </summary>
        internal void AddEmptyMarker(string itemType)
        {
            lock (_itemLists)
            {
                ErrorUtilities.VerifyThrow(!_itemLists.ContainsKey(itemType), "Should be none");
                _itemLists.Add(itemType, new LinkedList<T>());
            }
        }

        /// <summary>
        /// Special method used for batching buckets.
        /// Lookup can call this to see whether there was an explicit marker placed indicating that
        /// there are no items of this type. See comment on <see cref="AddEmptyMarker">AddEmptyMarker</see>.
        /// </summary>
        internal bool HasEmptyMarker(string itemType)
        {
            lock (_itemLists)
            {
                if (_itemLists.TryGetValue(itemType, out LinkedList<T> list) && list.Count == 0)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Custom enumerator that allows enumeration over all the items in the collection
        /// as though they were in a single list.
        /// All items of a type are returned consecutively in their correct order.
        /// However the order in which item types are returned is not defined.
        /// </summary>
        private sealed class Enumerator : IEnumerator<T>, IDisposable
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
            /// Finalizer
            /// </summary>
            ~Enumerator()
            {
                Dispose(false);
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
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// The real disposer.
            /// </summary>
            private void Dispose(bool disposing)
            {
                if (disposing)
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
