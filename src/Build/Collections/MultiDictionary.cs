// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A dictionary that can hold more than one distinct value with the same key.
    /// All keys must have at least one value: null values are currently rejected.
    /// </summary>
    /// <remarks>
    /// Order of values for a key is not defined but is currently the order of add.
    /// A variation could store the values in a HashSet, for different tradeoffs.
    /// </remarks>
    /// <typeparam name="K">Type of key</typeparam>
    /// <typeparam name="V">Type of value</typeparam>
    [DebuggerDisplay("#Keys={KeyCount} #Values={ValueCount}")]
    internal class MultiDictionary<K, V>
        where K : class
        where V : class
    {
        // The simplest implementation of MultiDictionary would use a Dictionary<K, List<V>>. 
        // However, a List<T> with one element is 44 bytes (empty, 24 bytes)
        // even though a single Object takes up only 12 bytes.
        // If most values are only one element, we can save space by storing Object
        // and using its implicit type field to discriminate.
        // 
        // Experiments, using a large number of keys:
        //
        // Dictionary<string,List<object>>, each key with one item, 127 bytes/key
        // Dictionary<string,List<object>>, each key with 1.01 items, 127 bytes/key
        // Dictionary<string,List<object>>, each key with 1.1 items, 128 bytes/key
        // Dictionary<string,List<object>>, each key with 1.5 items, 133 bytes/key
        // Dictionary<string,List<object>>, each key with 2 items, 139 bytes/key
        //
        // MultiDictionary<string, object>, each key with one item, 83 bytes/key
        // MultiDictionary<string, object>, each key with 1.01 items, 84 bytes/key
        // MultiDictionary<string, object>, each key with 1.1 item, 88 bytes/key
        // MultiDictionary<string, object>, each key with 1.5 items, 111 bytes/key
        // MultiDictionary<string, object>, each key with 2 items, 139 bytes/key
        //
        // Savings for 10,000 objects with 1.01 per entry is 420Kb out of 1.2Mb
        // If keys and values are already allocated (e.g., strings in use elsewhere) then this is 
        // the complete cost of the collection.

        /// <summary>
        /// Backing dictionary
        /// </summary>
        private Dictionary<K, SmallList<V>> _backing;

        /// <summary>
        /// Number of values over all keys
        /// </summary>
        private int _valueCount;

        /// <summary>
        /// Constructor taking a specified comparer for the keys
        /// </summary>
        internal MultiDictionary(IEqualityComparer<K> keyComparer)
        {
            _backing = new Dictionary<K, SmallList<V>>(keyComparer);
        }

        /// <summary>
        /// Number of keys
        /// </summary>
        internal int KeyCount => _backing.Count;

        /// <summary>
        /// Number of values over all keys
        /// </summary>
        internal int ValueCount => _valueCount;

        /// <summary>
        /// return keys in the dictionary
        /// </summary>
        internal IEnumerable<K> Keys => _backing.Keys;

        /// <summary>
        /// Enumerator over values that have the specified key.
        /// </summary>
        internal IEnumerable<V> this[K key]
        {
            get
            {
                if (!_backing.TryGetValue(key, out SmallList<V> entry))
                {
                    yield break;
                }

                foreach (V value in entry)
                {
                    yield return value;
                }
            }
        }

        /// <summary>
        /// Add a single value under the specified key.
        /// Value may not be null.
        /// </summary>
        internal void Add(K key, V value)
        {
            ErrorUtilities.VerifyThrow(value != null, "Null value not allowed");

            if (!_backing.TryGetValue(key, out SmallList<V> entry))
            {
                _backing.Add(key, new SmallList<V>(value));
            }
            else
            {
                entry.Add(value);
            }

            _valueCount++;
        }

        /// <summary>
        /// Removes an entry with the specified key and value.
        /// Returns true if found, false otherwise.
        /// </summary>
        internal bool Remove(K key, V value)
        {
            ErrorUtilities.VerifyThrow(value != null, "Null value not allowed");

            if (!_backing.TryGetValue(key, out SmallList<V> entry))
            {
                return false;
            }

            bool result = entry.Remove(value);

            if (result)
            {
                if (entry.Count == 0)
                {
                    _backing.Remove(key);
                }

                _valueCount--;
            }

            return result;
        }

        /// <summary>
        /// Empty the collection
        /// </summary>
        internal void Clear()
        {
            _backing = new Dictionary<K, SmallList<V>>();
            _valueCount = 0;
        }

        /// <summary>
        /// List capable of holding 0-n items.
        /// Uses less memory than List for less than 2 items.
        /// </summary>
        /// <typeparam name="TT">Type of the value</typeparam>
        private class SmallList<TT> : IEnumerable<TT>
            where TT : class
        {
            /// <summary>
            /// Entry - either a TT or a list of TT.
            /// </summary>
            private Object _entry;

            /// <summary>
            /// Constructor taking the initial object
            /// </summary>
            internal SmallList(TT first)
            {
                _entry = first;
            }

            /// <summary>
            /// Number of entries in this multivalue.
            /// </summary>
            internal int Count
            {
                get
                {
                    if (_entry == null)
                    {
                        return 0;
                    }

                    if (!(_entry is List<TT> list))
                    {
                        return 1;
                    }

                    return list.Count;
                }
            }

            /// <summary>
            /// Enumerable over the values.
            /// </summary>
            public IEnumerator<TT> GetEnumerator()
            {
                if (_entry == null)
                {
                    yield break;
                }
                else if (_entry is TT)
                {
                    yield return (TT)_entry;
                }
                else
                {
                    var list = _entry as List<TT>;

                    foreach (TT item in list)
                    {
                        yield return item;
                    }
                }
            }

            /// <summary>
            /// Enumerable over the values.
            /// </summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <summary>
            /// Add a value.
            /// Does not verify the value is not already present.
            /// </summary>
            public void Add(TT value)
            {
                if (_entry == null)
                {
                    _entry = value;
                }
                else if (_entry is TT)
                {
                    var list = new List<TT> { (TT) _entry, value };
                    _entry = list;
                }
                else
                {
                    var list = _entry as List<TT>;
                    list.Add(value);
                }
            }

            /// <summary>
            /// Remove a value.
            /// Returns true if the value existed, otherwise false.
            /// </summary>
            public bool Remove(TT value)
            {
                if (_entry == null)
                {
                    return false;
                }
                else if (_entry is TT)
                {
                    if (ReferenceEquals((TT)_entry, value))
                    {
                        _entry = null;
                        return true;
                    }

                    return false;
                }

                var list = _entry as List<TT>;

                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(value, list[i]))
                    {
                        if (list.Count == 2)
                        {
                            if (i == 0)
                            {
                                _entry = list[1];
                            }
                            else
                            {
                                _entry = list[0];
                            }
                        }
                        else
                        {
                            list.RemoveAt(i);
                        }

                        return true;
                    }
                }

                return false;
            }
        }
    }
}
