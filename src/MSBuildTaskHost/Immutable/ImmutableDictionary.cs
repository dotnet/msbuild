using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Collections.Immutable
{
    static class ImmutableExtensions
    {
        public static ImmutableDictionary<K,V> ToImmutableDictionary<K,V>(this IDictionary<K,V> dictionary)
        {
            return new ImmutableDictionary<K, V>(dictionary);
        }
    }

    class ImmutableDictionary
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
    class ImmutableDictionary<K, V>
    {
        /// <summary>
        /// The underlying dictionary.
        /// </summary>
        private Dictionary<K, V> _backing;

        //
        // READ-ONLY OPERATIONS
        //

        public ICollection<K> Keys
        {
            get
            {
                return _backing.Keys;
            }
        }

        public ICollection<V> Values
        {
            get
            {
                return _backing.Values;
            }
        }

        public int Count
        {
            get
            {
                return _backing.Count;
            }
        }

        public V this[K key]
        {
            get
            {
                return _backing[key];
            }
        }

        internal bool TryGetValue(K key, out V value)
        {
            return _backing.TryGetValue(key, out value);
        }

        internal bool Contains(KeyValuePair<K, V> item)
        {
            return _backing.Contains(item);
        }

        internal bool ContainsKey(K key)
        {
            return _backing.ContainsKey(key);
        }

        internal IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        //
        // WRITE OPERATIONS
        //

        internal ImmutableDictionary<K, V> Add(K key, V value)
        {
            var clone = new ImmutableDictionary<K, V>(_backing);
            clone._backing.Add(key, value);

            return clone;
        }

        internal ImmutableDictionary<K, V> Remove(K key)
        {
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

        internal ImmutableDictionary(IDictionary<K, V> source)
        {
            if (source is ImmutableDictionary<K, V> imm)
            {
                _backing = new Dictionary<K, V>(imm._backing, imm._backing.Comparer);
            }
            else
            {
                _backing = new Dictionary<K, V>(source);
            }
        }

        internal static ImmutableDictionary<K, V> Empty
        {
            get
            {
                return new ImmutableDictionary<K, V>();
            }
        }
    }
}
