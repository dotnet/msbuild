// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A special singleton enumerable that enumerates a read-only empty dictionary
    /// </summary>
    /// <typeparam name="K">Key</typeparam>
    /// <typeparam name="V">Value</typeparam>
    internal class ReadOnlyEmptyDictionary<K, V> : IDictionary<K, V>, IDictionary
    {
        /// <summary>
        /// The single instance
        /// </summary>
        private static readonly Dictionary<K, V> s_backing = new Dictionary<K, V>();

        /// <summary>
        /// The single instance
        /// </summary>
        private static ReadOnlyEmptyDictionary<K, V> s_instance;

        /// <summary>
        /// Private default constructor as this is a singleton
        /// </summary>
        private ReadOnlyEmptyDictionary()
        {
        }

        /// <summary>
        /// Get the instance
        /// </summary>
        public static ReadOnlyEmptyDictionary<K, V> Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new ReadOnlyEmptyDictionary<K, V>();
                }

                return s_instance;
            }
        }

        /// <summary>
        /// Empty returns zero
        /// </summary>
        public int Count
        {
            get { return 0; }
        }

        /// <summary>
        /// Returns true
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Gets empty collection
        /// </summary>
        public ICollection<K> Keys =>
#if CLR2COMPATIBILITY
            new K[0];
#else
            Array.Empty<K>();
#endif

        /// <summary>
        /// Gets empty collection
        /// </summary>
        public ICollection<V> Values =>
#if CLR2COMPATIBILITY
            new V[0];
#else
            Array.Empty<V>();
#endif

        /// <summary>
        /// Is it fixed size
        /// </summary>
        public bool IsFixedSize
        {
            get { return true; }
        }

        /// <summary>
        /// Not synchronized
        /// </summary>
        public bool IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// No sync root
        /// </summary>
        public object SyncRoot
        {
            get { return null; }
        }

        /// <summary>
        /// Keys
        /// </summary>
        ICollection IDictionary.Keys
        {
            get { return (ICollection)((IDictionary<K, V>)this).Keys; }
        }

        /// <summary>
        /// Values
        /// </summary>
        ICollection IDictionary.Values
        {
            get { return (ICollection)((IDictionary<K, V>)this).Values; }
        }

        /// <summary>
        /// Indexer
        /// </summary>
        public object this[object key]
        {
            get
            {
                return ((IDictionary<K, V>)this)[(K)key];
            }

            set
            {
                ((IDictionary<K, V>)this)[(K)key] = (V)value;
            }
        }

        /// <summary>
        /// Get returns null as read-only
        /// Set is prohibited and throws.
        /// </summary>
        public V this[K key]
        {
            get
            {
                // Trigger KeyNotFoundException
                return new Dictionary<K, V>()[key];
            }

            set
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            }
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        public void Add(K key, V value)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Empty returns false
        /// </summary>
        public bool ContainsKey(K key)
        {
            return false;
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public bool Remove(K key)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        /// <summary>
        /// Empty returns false
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            value = default(V);
            return false;
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public void Add(KeyValuePair<K, V> item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public void Clear()
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Empty returns false
        /// </summary>
        public bool Contains(KeyValuePair<K, V> item)
        {
            return false;
        }

        /// <summary>
        /// Empty does nothing
        /// </summary>
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public bool Remove(KeyValuePair<K, V> item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        /// <summary>
        /// Get empty enumerator
        /// </summary>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return Enumerable.Empty<KeyValuePair<K, V>>().GetEnumerator();
        }

        /// <summary>
        /// Get empty enumerator
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Add
        /// </summary>
        public void Add(object key, object value)
        {
            ((IDictionary<K, V>)this).Add((K)key, (V)value);
        }

        /// <summary>
        /// Contains
        /// </summary>
        public bool Contains(object key)
        {
            return ((IDictionary<K, V>)this).ContainsKey((K)key);
        }

        /// <summary>
        /// Enumerator
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)s_backing).GetEnumerator();
        }

        /// <summary>
        /// Remove
        /// </summary>
        public void Remove(object key)
        {
            ((IDictionary<K, V>)this).Remove((K)key);
        }

        /// <summary>
        /// CopyTo
        /// </summary>
        public void CopyTo(System.Array array, int index)
        {
            // Nothing to do
        }
    }
}
