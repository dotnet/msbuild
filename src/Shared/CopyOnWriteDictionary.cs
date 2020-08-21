// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A dictionary that has copy-on-write semantics.
    /// KEYS AND VALUES MUST BE IMMUTABLE OR COPY-ON-WRITE FOR THIS TO WORK.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    /// <remarks>
    /// Thread safety: for all users, this class is as thread safe as the underlying Dictionary implementation, that is,
    /// safe for concurrent readers or one writer from EACH user. It achieves this by locking itself and cloning before
    /// any write, if it is being shared - i.e., stopping sharing before any writes occur.
    /// </remarks>
    /// <comment>
    /// This class must be serializable as it is used for metadata passed to tasks, which may
    /// be run in a separate appdomain.
    /// </comment>
    [Serializable]
    internal class CopyOnWriteDictionary<K, V> : IDictionary<K, V>, IDictionary, ISerializable
    {
        /// <summary>
        /// The backing dictionary.
        /// Lazily created.
        /// </summary>
        private ImmutableDictionary<K, V> _backing;

        /// <summary>
        /// Constructor. Consider supplying a comparer instead.
        /// </summary>
        internal CopyOnWriteDictionary()
        {
            _backing = ImmutableDictionary<K, V>.Empty;
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
        internal CopyOnWriteDictionary(int capacity, IEqualityComparer<K>? keyComparer)
        {
            _backing = ImmutableDictionary.Create<K, V>(keyComparer);
        }

        /// <summary>
        /// Serialization constructor, for crossing appdomain boundaries
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "context", Justification = "Not needed")]
        protected CopyOnWriteDictionary(SerializationInfo info, StreamingContext context)
        {
            object v = info.GetValue(nameof(_backing), typeof(KeyValuePair<K, V>[]));

            object comparer = info.GetValue(nameof(Comparer), typeof(IEqualityComparer<K>));

            var b = ImmutableDictionary.Create<K, V>((IEqualityComparer<K>)comparer);

            _backing = b.AddRange((KeyValuePair<K, V>[])v);
        }

        /// <summary>
        /// Cloning constructor. Defers the actual clone.
        /// </summary>
        private CopyOnWriteDictionary(CopyOnWriteDictionary<K, V> that)
        {
            _backing = that._backing;
        }

        public CopyOnWriteDictionary(IDictionary<K, V> dictionary)
        {
            _backing = dictionary.ToImmutableDictionary();
        }

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<K> Keys => ((IDictionary<K, V>)_backing).Keys;

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<V> Values => ((IDictionary<K, V>)_backing).Values;

        /// <summary>
        /// Returns the number of items in the collection.
        /// </summary>
        public int Count => _backing.Count;

        /// <summary>
        /// Returns true if the collection is read-only.
        /// </summary>
        public bool IsReadOnly => ((IDictionary<K, V>)_backing).IsReadOnly;

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
        /// Comparer used for keys
        /// </summary>
        internal IEqualityComparer<K> Comparer
        {
            get => _backing.KeyComparer;
            private set => _backing = _backing.WithComparers(keyComparer: value);
        }

        /// <summary>
        /// Accesses the value for the specified key.
        /// </summary>
        public V this[K key]
        {
            get => _backing[key];

            set
            {
                _backing = _backing.SetItem(key, value);
            }
        }

#nullable disable
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
#nullable restore

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        public void Add(K key, V value)
        {
            _backing = _backing.SetItem(key, value);
        }

        /// <summary>
        /// Returns true if the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(K key)
        {
            return _backing.ContainsKey(key);
        }

        /// <summary>
        /// Removes the entry for the specified key from the dictionary.
        /// </summary>
        public bool Remove(K key)
        {
            ImmutableDictionary<K, V> initial = _backing;

            _backing = _backing.Remove(key);

            return initial != _backing; // whether the removal occured
        }

        /// <summary>
        /// Attempts to find the value for the specified key in the dictionary.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            return _backing.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        public void Add(KeyValuePair<K, V> item)
        {
            _backing = _backing.SetItem(item.Key, item.Value);
        }

        /// <summary>
        /// Clears the collection.
        /// </summary>
        public void Clear()
        {
            _backing = _backing.Clear();
        }

        /// <summary>
        /// Returns true ff the collection contains the specified item.
        /// </summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            return _backing.Contains(item);
        }

        /// <summary>
        /// Copies all of the elements of the collection to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            ((IDictionary<K, V>)_backing).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Remove an item from the dictionary.
        /// </summary>
        public bool Remove(KeyValuePair<K, V> item)
        {
            ImmutableDictionary<K, V> initial = _backing;

            _backing = _backing.Remove(item.Key);

            return initial != _backing; // whether the removal occured
        }

        /// <summary>
        /// Implementation of generic IEnumerable.GetEnumerator()
        /// </summary>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _backing.GetEnumerator();
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
            return ((IDictionary)_backing).GetEnumerator();
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
            return ReferenceEquals(other._backing, _backing);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ImmutableDictionary<K, V> snapshot = _backing;
            KeyValuePair<K, V>[] array = snapshot.ToArray();

            info.AddValue(nameof(_backing), array);
            info.AddValue(nameof(Comparer), Comparer);
        }
    }
}
