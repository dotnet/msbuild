// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A dictionary that does not hold strong references to either keys or values</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections;
using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Dictionary that does not prevent keys or values from being garbage collected.
    /// </summary>
    /// <remarks>
    /// Example scenarios:
    /// - store wrappers V around K, where given a K can find the V, but K and V can still be collected
    /// - store handles K to V, where given a K can find V, but K and V can still be collected
    /// PERF: Like a regular dictionary, the collection will never reduce its underlying size. This dictionary may be more
    /// prone to get overly capacious. If profiles show that is a problem, Scavenge could shrink the underlying dictionary
    /// based on some policy, e.g., if the number of remaining entries are less than 10% of the size of the underlying dictionary.
    /// </remarks>
    /// <typeparam name="K">Type of key</typeparam>
    /// <typeparam name="V">Type of value</typeparam>
    [DebuggerDisplay("#Entries={backingDictionary.Count}")]
    internal class WeakDictionary<K, V>
        where K : class
        where V : class
    {
        /// <summary>
        /// Backing dictionary
        /// </summary>
        private Dictionary<WeakReference<K>, WeakReference<V>> _backingDictionary;

        /// <summary>
        /// Improvised capacity. See comment in item setter.
        /// </summary>
        private int _capacity = 10;

        /// <summary>
        /// Constructor for a collection using the default key comparer
        /// </summary>
        internal WeakDictionary()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor taking a specified comparer for the keys
        /// </summary>
        internal WeakDictionary(IEqualityComparer<K> keyComparer)
        {
            IEqualityComparer<WeakReference<K>> equalityComparer = new WeakReferenceEqualityComparer<K>(keyComparer);
            _backingDictionary = new Dictionary<WeakReference<K>, WeakReference<V>>(equalityComparer);
        }

        /// <summary>
        /// Count of entries.
        /// Some entries may represent keys or values that have already been garbage collected.
        /// To clean these out call <see cref="Scavenge"/>.
        /// </summary>
        internal int Count
        {
            get { return _backingDictionary.Count; }
        }

        /// <summary>
        /// Gets or sets the entry whose key equates to the specified key.
        /// Getter throws KeyNotFoundException if key is not found.
        /// Setter adds entry if key is not found.
        /// </summary>
        /// <remarks>
        /// If we find the entry but its target is null, we take the opportunity
        /// to remove the entry, as if the GC had done it.
        /// </remarks>
        internal V this[K key]
        {
            get
            {
                WeakReference<K> wrappedKey = new WeakReference<K>(key);

                WeakReference<V> wrappedValue = _backingDictionary[wrappedKey];

                V value = wrappedValue.Target;

                if (value == null)
                {
                    Remove(key);

                    // Trigger KeyNotFoundException
                    wrappedValue = _backingDictionary[wrappedKey];

                    ErrorUtilities.ThrowInternalErrorUnreachable();
                }

                return value;
            }

            set
            {
                ErrorUtilities.VerifyThrowInternalNull(value, "value");

                WeakReference<K> wrappedKey = new WeakReference<K>(key);

                // Make some attempt to prevent dictionary growing forever with
                // entries whose underlying key or value has already been collected.
                // We do not have access to the dictionary's true capacity or growth
                // method, so we improvise with our own.
                // So attempt to make room for the upcoming add before we do it.
                if (_backingDictionary.Count == _capacity)
                {
                    Scavenge();

                    // If that didn't do anything, raise the capacity at which 
                    // we next scavenge. Note that we never shrink, but neither
                    // does the underlying dictionary.
                    if (_backingDictionary.Count == _capacity)
                    {
                        _capacity = _backingDictionary.Count * 2;
                    }
                }

                _backingDictionary[wrappedKey] = new WeakReference<V>(value);
            }
        }

        /// <summary>
        /// Whether there is a key present with the specified key
        /// </summary>
        /// <remarks>
        /// As usual, don't just call Contained as the wrapped value may be null.
        /// </remarks>
        internal bool Contains(K key)
        {
            V value;
            bool contained = TryGetValue(key, out value);

            return contained;
        }

        /// <summary>
        /// Attempts to get the value for the provided key.
        /// Returns true if the key is found, otherwise false.
        /// </summary>
        /// <remarks>
        /// If we find the entry but its target is null, we take the opportunity
        /// to remove the entry, as if the GC had done it.
        /// </remarks>
        internal bool TryGetValue(K key, out V value)
        {
            WeakReference<K> wrappedKey = new WeakReference<K>(key);

            WeakReference<V> wrappedValue;
            if (_backingDictionary.TryGetValue(wrappedKey, out wrappedValue))
            {
                value = wrappedValue.Target;

                if (value == null)
                {
                    Remove(key);
                    return false;
                }

                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Removes an entry with the specified key.
        /// Returns true if found, false otherwise.
        /// </summary>
        internal bool Remove(K key)
        {
            return _backingDictionary.Remove(new WeakReference<K>(key));
        }

        /// <summary>
        /// Remove any entries from the dictionary that represent keys or values
        /// that have been garbage collected.
        /// Returns the number of entries removed.
        /// </summary>
        internal int Scavenge()
        {
            List<WeakReference<K>> remove = null;

            foreach (KeyValuePair<WeakReference<K>, WeakReference<V>> pair in _backingDictionary)
            {
                // Get strong references to avoid GC races
                K keyTarget = pair.Key.Target;
                V valueTarget = pair.Value.Target;

                if (keyTarget == null || valueTarget == null)
                {
                    remove = remove ?? new List<WeakReference<K>>();

                    remove.Add(pair.Key);
                }
            }

            if (remove != null)
            {
                foreach (WeakReference<K> entry in remove)
                {
                    _backingDictionary.Remove(entry);
                }

                return remove.Count;
            }

            return 0;
        }

        /// <summary>
        /// Empty the collection
        /// </summary>
        internal void Clear()
        {
            _backingDictionary.Clear();
        }

        /// <summary>
        /// Equality comparer for weak references that actually compares the 
        /// targets of the weak references
        /// </summary>
        /// <typeparam name="T">Type of the targets of the weak references to be compared</typeparam>
        private class WeakReferenceEqualityComparer<T> : IEqualityComparer<WeakReference<T>>
            where T : class
        {
            /// <summary>
            /// Comparer to use if specified, otherwise null
            /// </summary>
            private IEqualityComparer<T> _underlyingComparer;

            /// <summary>
            /// Constructor to use an explicitly specified comparer.
            /// Comparer may be null, in which case the default comparer for the type
            /// will be used.
            /// </summary>
            internal WeakReferenceEqualityComparer(IEqualityComparer<T> comparer)
            {
                _underlyingComparer = comparer;
            }

            /// <summary>
            /// Gets the hashcode
            /// </summary>
            public int GetHashCode(WeakReference<T> item)
            {
                return item.GetHashCode();
            }

            /// <summary>
            /// Compares the weak references for equality
            /// </summary>
            public bool Equals(WeakReference<T> left, WeakReference<T> right)
            {
                if (Object.ReferenceEquals(left, right))
                {
                    return true;
                }

                if (Object.ReferenceEquals(left, null) || Object.ReferenceEquals(right, null))
                {
                    return false;
                }

                // Get strong references to targets to avoid GC race
                T leftTarget = left.Target;
                T rightTarget = right.Target;

                // Target(s) may have been collected
                if (Object.ReferenceEquals(leftTarget, rightTarget))
                {
                    return true;
                }

                if (Object.ReferenceEquals(leftTarget, null) || Object.ReferenceEquals(rightTarget, null))
                {
                    return false;
                }

                bool equals;

                if (_underlyingComparer != null)
                {
                    equals = _underlyingComparer.Equals(leftTarget, rightTarget);
                }
                else
                {
                    // Compare using target's own equality operator
                    equals = leftTarget.Equals(rightTarget);
                }

                return equals;
            }
        }
    }
}
