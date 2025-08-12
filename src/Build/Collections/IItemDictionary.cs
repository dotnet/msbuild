// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    internal interface IItemDictionary<T> : IEnumerable<T>, IItemProvider<T>
        where T : class, IKeyed, IItem
    {
        /// <summary>
        /// Number of items in total, for debugging purposes.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Get the item types that have at least one item in this collection.
        /// </summary>
        /// <remarks>
        /// KeyCollection&lt;K&gt; is already a read only collection, so no protection
        /// is necessary.
        /// </remarks>
        ICollection<string> ItemTypes { get; }

        /// <summary>
        /// Returns the item list for a particular item type,
        /// creating and adding a new item list if necessary.
        /// Does not throw if there are no items of this type.
        /// This is a read-only list.
        /// If the result is not empty it is a live list.
        /// Use AddItem or RemoveItem to modify items in this project.
        /// Using the return value from this in a multithreaded situation is unsafe.
        /// </summary>
        ICollection<T> this[string itemType] { get; }

        /// <summary>
        /// Empty the collection.
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns an enumerable which copies the underlying data on read.
        /// </summary>
        IEnumerable<TResult> GetCopyOnReadEnumerable<TResult>(Func<T, TResult> selector);

        /// <summary>
        /// Enumerates item lists per each item type under the lock.
        /// </summary>
        /// <param name="itemTypeCallback">
        /// A delegate that accepts the item type string and a list of items of that type.
        /// Will be called for each item type in the list.
        /// </param>
        void EnumerateItemsPerType(Action<string, IEnumerable<T>> itemTypeCallback);

        /// <summary>
        /// Whether the provided item is in this table or not.
        /// </summary>
        bool Contains(T projectItem);

        /// <summary>
        /// Add a new item to the collection, at the
        /// end of the list of other items with its key.
        /// </summary>
        void Add(T projectItem);

        /// <summary>
        /// Adds each new item to the collection, at the
        /// end of the list of other items with the same key.
        /// </summary>
        void AddRange(IEnumerable<T> projectItems);

        /// <summary>
        /// Removes an item, if it is in the collection.
        /// Returns true if it was found, otherwise false.
        /// </summary>
        /// <remarks>
        /// If a list is emptied, removes the list from the enclosing collection
        /// so it can be garbage collected.
        /// </remarks>        
        bool Remove(T projectItem);

        /// <summary>
        /// Replaces an existing item with a new item.  This is necessary to preserve the original ordering semantics of Lookup.GetItems
        /// when items with metadata modifications are being returned.  See Dev10 bug 480737.
        /// If the item is not found, does nothing.
        /// </summary>
        /// <param name="existingItem">The item to be replaced.</param>
        /// <param name="newItem">The replacement item.</param>
        void Replace(T existingItem, T newItem);

        /// <summary>
        /// Add the set of items specified to this dictionary.
        /// </summary>
        /// <param name="other">An enumerator over the items to remove.</param>
        void ImportItems(IEnumerable<T> other);

        /// <summary>
        /// Add the set of items specified, all sharing an item type, to this dictionary.
        /// </summary>
        /// <comment>
        /// This is a little faster than ImportItems where all the items have the same item type.
        /// </comment>
        void ImportItemsOfType(string itemType, IEnumerable<T> items);

        /// <summary>
        /// Remove the set of items specified from this dictionary
        /// </summary>
        /// <param name="other">An enumerator over the items to remove.</param>
        void RemoveItems(IEnumerable<T> other);

        /// <summary>
        /// Special method used for batching buckets.
        /// Adds an explicit marker indicating there are no items for the specified item type.
        /// In the general case, this is redundant, but batching buckets use this to indicate that they are
        /// batching over the item type, but their bucket does not contain items of that type.
        /// See <see cref="HasEmptyMarker">HasEmptyMarker</see>.
        /// </summary>
        void AddEmptyMarker(string itemType);

        /// <summary>
        /// Special method used for batching buckets.
        /// Lookup can call this to see whether there was an explicit marker placed indicating that
        /// there are no items of this type. See comment on <see cref="AddEmptyMarker">AddEmptyMarker</see>.
        /// </summary>
        bool HasEmptyMarker(string itemType);
    }
}
