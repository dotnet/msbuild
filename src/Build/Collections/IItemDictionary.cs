// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;

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
        /// Add a new item to the collection, at the
        /// end of the list of other items with its key.
        /// </summary>
        void Add(T projectItem);

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
        /// <param name="itemType">The item type for all removes.</param>
        /// <param name="other">An enumerator over the items to remove.</param>
        void RemoveItemsOfType(string itemType, IEnumerable<T> other);
    }
}
