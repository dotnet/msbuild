// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace System.Collections.Immutable
{
    static class ImmutableExtensions
    {
        public static ImmutableDictionary<K,V> ToImmutableDictionary<K,V>(this IDictionary<K,V> dictionary)
        {
            return new ImmutableDictionary<K, V>(dictionary);
        }
    }

    static class ImmutableDictionary
    {
        internal static ImmutableDictionary<K, V> Create<K, V>(IEqualityComparer<K> comparer)
        {
            return new ImmutableDictionary<K, V>(comparer);
        }
    }

    /// <summary>
    /// Inefficient ImmutableDictionary implementation: keep a mutable dictionary and wrap all operations.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    sealed class ImmutableDictionary<K, V> : IDictionary<K, V>, IDictionary
    {
        /// <summary>
        /// The underlying dictionary.
        /// </summary>
        private Dictionary<K, V> _backing;

        #region Read-only Operations

        public ICollection<K> Keys => _backing.Keys;
        public ICollection<V> Values => _backing.Values;

        ICollection IDictionary.Keys => _backing.Keys;
        ICollection IDictionary.Values => _backing.Values;

        public int Count => _backing.Count;

        public V this[K key] => _backing[key];

        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        public bool IsSynchronized => true;

        public object SyncRoot => this;

        public bool TryGetValue(K key, out V value)
        {
            return _backing.TryGetValue(key, out value);
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            return _backing.Contains(item);
        }

        bool IDictionary.Contains(object key)
        {
            return ((IDictionary)_backing).Contains(key);
        }

        public bool ContainsKey(K key)
        {
            return _backing.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        void ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            CheckCopyToArguments(array, arrayIndex);
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            CheckCopyToArguments(array, arrayIndex);
            foreach (var item in this)
            {
                array.SetValue(new DictionaryEntry(item.Key, item.Value), arrayIndex++);
            }
        }

        private void CheckCopyToArguments(Array array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }
            if (arrayIndex + Count > array.Length)
            {
                throw new ArgumentException(nameof(arrayIndex));
            }
        }

        #endregion

        #region Write Operations

        internal ImmutableDictionary<K, V> SetItem(K key, V value)
        {
            if (TryGetValue(key, out V existingValue) && Object.Equals(existingValue, value))
            {
                return this;
            }

            var clone = new ImmutableDictionary<K, V>(_backing);
            clone._backing[key] = value;

            return clone;
        }

        internal ImmutableDictionary<K, V> Remove(K key)
        {
            if (!ContainsKey(key))
            {
                return this;
            }

            var clone = new ImmutableDictionary<K, V>(_backing);
            clone._backing.Remove(key);

            return clone;
        }

        internal ImmutableDictionary<K, V> Clear()
        {
            return new ImmutableDictionary<K, V>(_backing.Comparer);
        }

        internal ImmutableDictionary()
        {
            _backing = new Dictionary<K, V>();
        }

        internal ImmutableDictionary(IEqualityComparer<K> comparer)
        {
            _backing = new Dictionary<K, V>(comparer);
        }

        internal ImmutableDictionary(IDictionary<K, V> source, IEqualityComparer<K> keyComparer = null)
        {
            if (source is ImmutableDictionary<K, V> imm)
            {
                _backing = new Dictionary<K, V>(imm._backing, keyComparer ?? imm._backing.Comparer);
            }
            else
            {
                _backing = new Dictionary<K, V>(source, keyComparer);
            }
        }

        internal static ImmutableDictionary<K, V> Empty
        {
            get
            {
                return new ImmutableDictionary<K, V>();
            }
        }

        public IEqualityComparer<K> KeyComparer { get => _backing.Comparer; internal set => throw new NotSupportedException(); }

        internal KeyValuePair<K, V>[] ToArray()
        {
            return _backing.ToArray();
        }

        internal ImmutableDictionary<K, V> AddRange(KeyValuePair<K, V>[] v)
        {
            var n = new Dictionary<K, V>(_backing, _backing.Comparer);

            foreach (var item in v)
            {
                n.Add(item.Key, item.Value);
            }

            return new ImmutableDictionary<K, V>(n);
        }

        internal ImmutableDictionary<K, V> WithComparers(IEqualityComparer<K> keyComparer)
        {
            return new ImmutableDictionary<K, V>(_backing, keyComparer);
        }

        #endregion

        #region Unsupported Operations

        object IDictionary.this[object key]
        {
            get { return _backing[(K)key]; }
            set { throw new NotSupportedException(); }
        }

        void IDictionary.Add(object key, object value)
        {
            throw new NotSupportedException();
        }

        void IDictionary.Remove(object key)
        {
            throw new NotSupportedException();
        }

        void IDictionary.Clear()
        {
            throw new NotSupportedException();
        }

        V IDictionary<K, V>.this[K key]
        {
            get { return _backing[key]; }
            set { throw new NotSupportedException(); }
        }

        void IDictionary<K, V>.Add(K key, V value)
        {
            throw new NotSupportedException();
        }

        bool IDictionary<K, V>.Remove(K key)
        {
            throw new NotSupportedException();
        }

        void ICollection<KeyValuePair<K, V>>.Add(KeyValuePair<K, V> item)
        {
            throw new NotSupportedException();
        }

        void ICollection<KeyValuePair<K, V>>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
