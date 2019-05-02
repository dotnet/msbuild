// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A dictionary that has copy-on-write semantics.
    /// KEYS AND VALUES MUST BE IMMUTABLE OR COPY-ON-WRITE FOR THIS TO WORK.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    /// <remarks>
    /// This dictionary works by having a backing dictionary which is ref-counted for each
    /// COWDictionary which references it.  When a write operation is performed on any
    /// COWDictionary, we check the reference count on the backing dictionary.  If it is 
    /// greater than 1, it means any changes we make to it would be visible to other readers.
    /// Therefore, we clone the backing dictionary and decrement the reference count on the
    /// original.  From there on we use the cloned dictionary, which now has a reference count
    /// of 1.
    ///
    /// Thread safety: for all users, this class is as thread safe as the underlying Dictionary implementation, that is,
    /// safe for concurrent readers or one writer from EACH user. It achieves this by locking itself and cloning before
    /// any write, if it is being shared - i.e., stopping sharing before any writes occur.
    /// </remarks>
    /// <comment>
    /// This class must be serializable as it is used for metadata passed to tasks, which may
    /// be run in a separate appdomain.
    /// </comment>
    [Serializable]
    internal class CopyOnWriteDictionary<K, V> : IDictionary<K, V>, IDictionary where V : class
    {
        /// <summary>
        /// The backing dictionary.
        /// Lazily created.
        /// </summary>
        private ImmutableDictionary<K, V> backing;

        /// <summary>
        /// Constructor. Consider supplying a comparer instead.
        /// </summary>
        internal CopyOnWriteDictionary()
        {
        }

        /// <summary>
        /// Constructor taking an initial capacity
        /// </summary>
        internal CopyOnWriteDictionary(int capacity)
            : this(capacity, null)
        {
        }

        /// <summary>
        /// Constructor taking a specified comparer for the keys
        /// </summary>
        internal CopyOnWriteDictionary(IEqualityComparer<K> keyComparer)
            : this(0, keyComparer)
        {
        }

        /// <summary>
        /// Constructor taking a specified comparer for the keys and an initial capacity
        /// </summary>
        internal CopyOnWriteDictionary(int capacity, IEqualityComparer<K> keyComparer)
        {
            Comparer = keyComparer;
        }

        /// <summary>
        /// Serialization constructor, for crossing appdomain boundaries
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "info", Justification = "Not needed")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "context", Justification = "Not needed")]
        protected CopyOnWriteDictionary(SerializationInfo info, StreamingContext context)
        {
        }

        /// <summary>
        /// Cloning constructor. Defers the actual clone.
        /// </summary>
        private CopyOnWriteDictionary(CopyOnWriteDictionary<K, V> that)
        {
            Comparer = that.Comparer;
            backing = that.backing;
        }

        public CopyOnWriteDictionary(IDictionary<K, V> dictionary)
        {
            backing = dictionary.ToImmutableDictionary();
        }

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<K> Keys => (ICollection<K>)ReadOperation.Keys;

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<V> Values => (ICollection<V>)ReadOperation.Values;

        /// <summary>
        /// Returns the number of items in the collection.
        /// </summary>
        public int Count => ReadOperation.Count;

        /// <summary>
        /// Returns true if the collection is read-only.
        /// </summary>
        public bool IsReadOnly => ((IDictionary<K, V>)ReadOperation).IsReadOnly;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        bool IDictionary.IsFixedSize => false;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        bool IDictionary.IsReadOnly => IsReadOnly;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        ICollection IDictionary.Keys => (ICollection)Keys;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        ICollection IDictionary.Values => (ICollection)Values;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        int ICollection.Count => Count;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        bool ICollection.IsSynchronized => false;

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        object ICollection.SyncRoot => this;

        /// <summary>
        /// A special single dummy instance that always appears empty.
        /// </summary>
        internal static CopyOnWriteDictionary<K, V> Dummy { get; } = new CopyOnWriteDictionary<K, V> { _isDummy = true };

        /// <summary>
        /// Whether this is a dummy instance that always appears empty.
        /// </summary>
        internal bool IsDummy
        {
            get
            {
                if (_isDummy)
                {
                    ErrorUtilities.VerifyThrow(backing == null || backing.Count == 0, "count"); // check count without recursion
                }

                return _isDummy;
            }
        }

        private bool _isDummy;

        /// <summary>
        /// Comparer used for keys
        /// </summary>
        internal IEqualityComparer<K> Comparer { get; private set; }

        /// <summary>
        /// Gets the backing dictionary for reading.
        /// </summary>
        private ImmutableDictionary<K, V> ReadOperation
        {
            get
            {
                ErrorUtilities.VerifyThrow(!IsDummy || backing == null || backing.Count == 0, "count"); // check count without recursion

                return backing ?? ImmutableDictionary<K,V>.Empty;
            }
        }

        /// <summary>
        /// Gets the backing dictionary for writing.
        /// </summary>
        private ImmutableDictionary<K, V> WriteOperation
        {
            get
            {
                ErrorUtilities.VerifyThrow(!IsDummy, "dummy");

                if (backing == null)
                {
                    backing = ImmutableDictionary.Create<K,V>(Comparer);
                }

                return backing;
            }
        }

        /// <summary>
        /// Accesses the value for the specified key.
        /// </summary>
        public V this[K key]
        {
            get => ReadOperation[key];

            set
            {
                if (!IsDummy)
                {
                    backing = WriteOperation.SetItem(key, value);
                }
            }
        }

        /// <summary>
        /// IDictionary implementation
        /// </summary>
        object IDictionary.this[object key]
        {
            get
            {
                TryGetValue((K) key, out V val);
                return val;
            }

            set => this[(K)key] = (V)value;
        }

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        public void Add(K key, V value)
        {
            if (!IsDummy)
            {
                backing = WriteOperation.SetItem(key, value);
            }
        }

        /// <summary>
        /// Returns true if the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(K key)
        {
            return ReadOperation.ContainsKey(key);
        }

        /// <summary>
        /// Removes the entry for the specified key from the dictionary.
        /// </summary>
        public bool Remove(K key)
        {
            ImmutableDictionary<K, V> initial = backing;

            backing = backing.Remove(key);

            return initial != backing; // if the removal occured, the 
        }

        /// <summary>
        /// Attempts to find the value for the specified key in the dictionary.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            return ReadOperation.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        public void Add(KeyValuePair<K, V> item)
        {
            if (!IsDummy)
            {
                backing = backing.SetItem(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Clears the collection.
        /// </summary>
        public void Clear()
        {
            if (!IsDummy)
            {
                backing = backing.Clear();
            }
        }

        /// <summary>
        /// Returns true ff the collection contains the specified item.
        /// </summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            return ReadOperation.Contains(item);
        }

        /// <summary>
        /// Copies all of the elements of the collection to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            ((IDictionary<K, V>)ReadOperation).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Remove an item from the dictionary.
        /// </summary>
        public bool Remove(KeyValuePair<K, V> item)
        {
            ImmutableDictionary<K, V> initial = backing;

            backing = backing.Remove(item.Key);

            return initial != backing; // if the removal occured, the 
        }

        /// <summary>
        /// Implementation of generic IEnumerable.GetEnumerator()
        /// </summary>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return ReadOperation.GetEnumerator();
        }

        /// <summary>
        /// Implementation of IEnumerable.GetEnumerator()
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<K, V>>)this).GetEnumerator();
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void IDictionary.Add(object key, object value)
        {
            Add((K)key, (V)value);
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void IDictionary.Clear()
        {
            Clear();
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        bool IDictionary.Contains(object key)
        {
            return ContainsKey((K)key);
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)ReadOperation).GetEnumerator();
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void IDictionary.Remove(object key)
        {
            Remove((K)key);
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        {
            int i = 0;
            foreach (KeyValuePair<K, V> entry in this)
            {
                array.SetValue(new DictionaryEntry(entry.Key, entry.Value), index + i);
                i++;
            }
        }

        /// <summary>
        /// Clone, with the actual clone deferred
        /// </summary>
        internal CopyOnWriteDictionary<K, V> Clone()
        {
            return new CopyOnWriteDictionary<K, V>(this);
        }

        /// <summary>
        /// Returns true if these dictionaries have the same backing.
        /// </summary>
        internal bool HasSameBacking(CopyOnWriteDictionary<K, V> other)
        {
            return ReferenceEquals(other.backing, backing);
        }
    }
}
