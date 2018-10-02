// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Implementation of a dictionary which acts as a read-only wrapper on another dictionary, but
    /// converts values as they are accessed to another type.
    /// </summary>
    /// <typeparam name="K">The backing dictionary's key type.</typeparam>
    /// <typeparam name="V">The backing dictionary's value type.</typeparam>
    /// <typeparam name="N">The desired value type.</typeparam>
    internal class ReadOnlyConvertingDictionary<K, V, N> : IDictionary<K, N>
    {
        /// <summary>
        /// The backing dictionary.
        /// </summary>
        private readonly IDictionary<K, V> _backing;

        /// <summary>
        /// The delegate used to convert values.
        /// </summary>
        private readonly Func<V, N> _converter;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal ReadOnlyConvertingDictionary(IDictionary<K, V> backing, Func<V, N> converter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(backing, "backing");
            ErrorUtilities.VerifyThrowArgumentNull(converter, "converter");

            _backing = backing;
            _converter = converter;
        }

        #region IDictionary<string,string> Members

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<K> Keys => _backing.Keys;

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<N> Values
        {
            get
            {
                ErrorUtilities.ThrowInternalError("Values is not supported on ReadOnlyConvertingDictionary.");

                // Show the compiler that this always throws:
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the number of items in the collection.
        /// </summary>
        public int Count => _backing.Count;

        /// <summary>
        /// Returns true if the collection is read-only.
        /// </summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Accesses the value for the specified key.
        /// </summary>
        public N this[K key]
        {
            get => _converter(_backing[key]);
            set => ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        public void Add(K key, N value)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
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
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        /// <summary>
        /// Attempts to find the value for the specified key in the dictionary.
        /// </summary>
        public bool TryGetValue(K key, out N value)
        {
            if (_backing.TryGetValue(key, out V originalValue))
            {
                value = _converter(originalValue);
                return true;
            }

            value = default(N);
            return false;
        }

        #endregion

        #region ICollection<KeyValuePair<string,string>> Members

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        public void Add(KeyValuePair<K, N> item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Clears the collection.
        /// </summary>
        public void Clear()
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Returns true ff the collection contains the specified item.
        /// </summary>
        public bool Contains(KeyValuePair<K, N> item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedConvertingCollectionValueToBacking");
            return false;
        }

        /// <summary>
        /// Copies all of the elements of the collection to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<K, N>[] array, int arrayIndex)
        {
            ErrorUtilities.VerifyThrow(array.Length - arrayIndex >= _backing.Count, "Specified array size insufficient to hold the contents of the collection.");

            foreach (KeyValuePair<K, V> pair in _backing)
            {
                array[arrayIndex++] = KeyValueConverter(pair);
            }
        }

        /// <summary>
        /// Remove an item from the dictionary.
        /// </summary>
        public bool Remove(KeyValuePair<K, N> item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        #endregion

        #region IEnumerable<KeyValuePair<K, N>> Members

        /// <summary>
        /// Implementation of generic IEnumerable.GetEnumerator()
        /// </summary>
        public IEnumerator<KeyValuePair<K, N>> GetEnumerator()
        {
            return new ConvertingEnumerable<KeyValuePair<K, V>, KeyValuePair<K, N>>(_backing, KeyValueConverter).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Implementation of IEnumerable.GetEnumerator()
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<K, N>>)this).GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Delegate used by ConvertingEnumerable
        /// </summary>
        private KeyValuePair<K, N> KeyValueConverter(KeyValuePair<K, V> original)
        {
            return new KeyValuePair<K, N>(original.Key, _converter(original.Value));
        }
    }
}
