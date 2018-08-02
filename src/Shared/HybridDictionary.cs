// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// HybridDictionary is a dictionary which is implemented to efficiently store both small and large numbers of items.  When only a single item is stored, we use no 
    /// collections at all.  When 1 &lt; n &lt;= MaxListSize is stored, we use a list.  For any larger number of elements, we use a dictionary.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    [Serializable]
    internal class HybridDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, ICollection where TValue : class
    {
        /// <summary>
        /// The maximum number of entries we will store in a list before converting it to a dictionary.
        /// </summary>
        internal static readonly int MaxListSize = 15;

        /// <summary>
        /// The dictionary, list, or pair used for a store
        /// </summary>
        private Object store;

        /// <summary>
        /// The comparer used to look up an item.
        /// </summary>
        private IEqualityComparer<TKey> comparer;

        /// <summary>
        /// Static constructor
        /// </summary>
        static HybridDictionary()
        {
            int value;
            if (Int32.TryParse(Environment.GetEnvironmentVariable("MSBuildHybridDictThreshold"), out value))
            {
                MaxListSize = value;
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HybridDictionary()
            : this(0)
        {
        }

        /// <summary>
        /// Capacity constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity of the collection.</param>
        public HybridDictionary(int capacity)
            : this(capacity, EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="comparer">The comparer to use.</param>
        public HybridDictionary(IEqualityComparer<TKey> comparer)
            : this()
        {
            this.comparer = comparer;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <param name="comparer">The comparer to use.</param>
        public HybridDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            this.comparer = comparer;
            if (this.comparer == null)
            {
                this.comparer = EqualityComparer<TKey>.Default;
            }

            if (capacity > MaxListSize)
            {
                store = new Dictionary<TKey, TValue>(capacity, comparer);
            }
            else if (capacity > 1)
            {
                store = new List<KeyValuePair<TKey, TValue>>(capacity);
            }
        }

        /// <summary>
        /// Serialization constructor.
        /// </summary>
        public HybridDictionary(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cloning constructor.
        /// </summary>
        public HybridDictionary(HybridDictionary<TKey, TValue> other, IEqualityComparer<TKey> comparer)
            : this(other.Count, comparer)
        {
            foreach (KeyValuePair<TKey, TValue> keyValue in other)
            {
                Add(keyValue.Key, keyValue.Value);
            }
        }

        /// <summary>
        /// Gets the comparer used to compare keys.
        /// </summary>
        public IEqualityComparer<TKey> Comparer
        {
            get { return comparer; }
        }

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                if (store == null)
                {
                    return ReadOnlyEmptyCollection<TKey>.Instance;
                }

                if (store is KeyValuePair<TKey, TValue>)
                {
                    return new TKey[] { ((KeyValuePair<TKey, TValue>)store).Key };
                }

                var list = store as List<KeyValuePair<TKey, TValue>>;
                if (list != null)
                {
                    TKey[] keys = new TKey[list.Count];
                    for (int i = 0; i < list.Count; i++)
                    {
                        keys[i] = list[i].Key;
                    }

                    return keys;
                }

                var dictionary = store as Dictionary<TKey, TValue>;
                if (dictionary != null)
                {
                    return dictionary.Keys;
                }

                ErrorUtilities.ThrowInternalErrorUnreachable();
                return null;
            }
        }

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                if (store == null)
                {
                    return ReadOnlyEmptyCollection<TValue>.Instance;
                }

                if (store is KeyValuePair<TKey, TValue>) // Can't use 'as' for structs
                {
                    return new TValue[] { ((KeyValuePair<TKey, TValue>)store).Value };
                }

                var list = store as List<KeyValuePair<TKey, TValue>>;
                if (list != null)
                {
                    TValue[] values = new TValue[list.Count];
                    for (int i = 0; i < list.Count; i++)
                    {
                        values[i] = list[i].Value;
                    }

                    return values;
                }

                var dictionary = store as Dictionary<TKey, TValue>;
                if (dictionary != null)
                {
                    return dictionary.Values;
                }

                ErrorUtilities.ThrowInternalErrorUnreachable();
                return null;
            }
        }

        /// <summary>
        /// Gets the number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                if (store == null)
                {
                    return 0;
                }

                if (store is KeyValuePair<TKey, TValue>)
                {
                    return 1;
                }

                return ((ICollection)store).Count;
            }
        }

        /// <summary>
        /// Returns true if this is a read-only collection.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Returns true if this collection is synchronized.
        /// </summary>
        public bool IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the sync root for this collection.
        /// </summary>
        /// <remarks>
        /// NOTE: Returns "this", which is not normally recommended as a caller
        /// could implement its own locking scheme on "this" and deadlock. However, a
        /// sync object would be significant wasted space as there are a lot of these, 
        /// and the caller is not foolish.
        /// </remarks>
        public object SyncRoot
        {
            get { return this; }
        }

        /// <summary>
        /// Returns true if the dictionary is a fixed size.
        /// </summary>
        public bool IsFixedSize
        {
            get { return false; }
        }

        /// <summary>
        /// Returns a collection of the keys in the dictionary.
        /// </summary>
        ICollection IDictionary.Keys
        {
            get { return (ICollection)((IDictionary<TKey, TValue>)this).Keys; }
        }

        /// <summary>
        /// Returns a collection of the values in the dictionary.
        /// </summary>
        ICollection IDictionary.Values
        {
            get { return (ICollection)((IDictionary<TKey, TValue>)this).Values; }
        }

        /// <summary>
        /// Item accessor.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (TryGetValue(key, out value))
                {
                    return value;
                }

                throw new KeyNotFoundException("The specified key was not found in the collection.");
            }

            set
            {
                if (store == null)
                {
                    store = new KeyValuePair<TKey, TValue>(key, value);
                    return;
                }

                if (store is KeyValuePair<TKey, TValue>)
                {
                    var single = (KeyValuePair<TKey, TValue>)store;
                    if (comparer.Equals(single.Key, key))
                    {
                        store = new KeyValuePair<TKey, TValue>(key, value);
                        return;
                    }

                    store = new List<KeyValuePair<TKey, TValue>> { { single }, { new KeyValuePair<TKey, TValue>(key, value) } };
                    return;
                }

                var list = store as List<KeyValuePair<TKey, TValue>>;
                if (list != null)
                {
                    AddToOrUpdateList(list, key, value, throwIfPresent: false);
                    return;
                }

                var dictionary = store as Dictionary<TKey, TValue>;
                if (dictionary != null)
                {
                    dictionary[key] = value;
                    return;
                }

                ErrorUtilities.ThrowInternalErrorUnreachable();
            }
        }

        /// <summary>
        /// Item accessor.
        /// </summary>
        public object this[object key]
        {
            get { return ((IDictionary<TKey, TValue>)this)[(TKey)key]; }
            set { ((IDictionary<TKey, TValue>)this)[(TKey)key] = (TValue)value; }
        }

        /// <summary>
        /// Adds an item to the dictionary.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            ErrorUtilities.VerifyThrowArgumentNull(key, nameof(key));

            if (store == null)
            {
                store = new KeyValuePair<TKey, TValue>(key, value);
                return;
            }

            if (store is KeyValuePair<TKey, TValue>)
            {
                var single = (KeyValuePair<TKey, TValue>)store;
                if (comparer.Equals(single.Key, key))
                {
                    throw new ArgumentException("A value with the same key is already in the collection.");
                }

                store = new List<KeyValuePair<TKey, TValue>> { { single }, { new KeyValuePair<TKey, TValue>(key, value) } };
                return;
            }

            var list = store as List<KeyValuePair<TKey, TValue>>;
            if (list != null)
            {
                AddToOrUpdateList(list, key, value, throwIfPresent: true);
                return;
            }

            var dictionary = store as Dictionary<TKey, TValue>;
            if (dictionary != null)
            {
                dictionary.Add(key, value);
                return;
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        /// <summary>
        /// Returns true if the specified key is contained within the dictionary.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            TValue discard;
            return TryGetValue(key, out discard);
        }

        /// <summary>
        /// Removes a key from the dictionary.
        /// </summary>
        public bool Remove(TKey key)
        {
            ErrorUtilities.VerifyThrowArgumentNull(key, nameof(key));

            if (store == null)
            {
                return false;
            }

            if (store is KeyValuePair<TKey, TValue>)
            {
                if (comparer.Equals(((KeyValuePair<TKey, TValue>)store).Key, key))
                {
                    store = null;
                    return true;
                }

                return false;
            }

            if (store is List<KeyValuePair<TKey, TValue>> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (comparer.Equals(list[i].Key, key))
                    {
                        list.RemoveAt(i); // POLICY: copy into new shorter list
                        return true;
                    }
                }

                return false;
            }

            if (store is Dictionary<TKey, TValue> dictionary)
            {
                return dictionary.Remove(key);
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();
            return false;
        }

        /// <summary>
        /// Returns true and the value for the specified key if it is present in the dictionary, false otherwise.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = null;

            switch (store)
            {
                case null:
                {
                    return false;
                }
                case KeyValuePair<TKey, TValue> pair:
                {
                    if (comparer.Equals(pair.Key, key))
                    {
                        value = pair.Value;
                        return true;
                    }

                    return false;
                }
                case List<KeyValuePair<TKey, TValue>> list:
                {
                    foreach (KeyValuePair<TKey, TValue> entry in list)
                    {
                        if (comparer.Equals(entry.Key, key))
                        {
                            value = entry.Value;
                            return true;
                        }
                    }

                    return false;
                }
                case Dictionary<TKey, TValue> dictionary:
                {
                    return dictionary.TryGetValue(key, out value);
                }
                default:
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return false;
                }
            }
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary.
        /// </summary>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <summary>
        /// Clears the dictionary.
        /// </summary>
        public void Clear() => store = null;

        /// <summary>
        /// Returns true of the dictionary contains the key/value pair.
        /// </summary>
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out TValue value) && item.Value == value;

        /// <summary>
        /// Copies the contents of the dictionary to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue> entry in this)
            {
                array[i] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Removed the specified key/value pair from the dictionary.
        /// NOT IMPLEMENTED.
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

        /// <summary>
        /// Gets an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            switch (store)
            {
                case null:
                {
                    return ReadOnlyEmptyCollection<KeyValuePair<TKey, TValue>>.Instance.GetEnumerator();
                }
                case KeyValuePair<TKey, TValue> pair:
                {
                    return new SingleEnumerator(pair);
                }
                case List<KeyValuePair<TKey, TValue>> list:
                {
                    return list.GetEnumerator();
                }
                case Dictionary<TKey, TValue> dictionary:
                {
                    return dictionary.GetEnumerator();
                }
                default:
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Copies the contents of the dictionary to the specified Array.
        /// </summary>
        public void CopyTo(Array array, int index)
        {
            int i = index;
            foreach (KeyValuePair<TKey, TValue> entry in this)
            {
                array.SetValue(new DictionaryEntry(entry.Key, entry.Value), i);
            }
        }

        /// <summary>
        /// Adds the specified key/value pair to the dictionary.
        /// </summary>
        public void Add(object key, object value) => Add((TKey)key, (TValue)value);

        /// <summary>
        /// Returns true if the dictionary contains the specified key.
        /// </summary>
        public bool Contains(object key) => ContainsKey((TKey)key);

        /// <summary>
        /// Returns an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            switch (store)
            {
                case null:
                {
                    return ((IDictionary) ReadOnlyEmptyDictionary<TKey, TValue>.Instance).GetEnumerator();
                }
                case KeyValuePair<TKey, TValue> pair:
                {
                    return new SingleDictionaryEntryEnumerator(new DictionaryEntry(pair.Key, pair.Value));
                }
                case List<KeyValuePair<TKey, TValue>> list:
                {
                    return new ListDictionaryEntryEnumerator<TKey, TValue>(list);
                }
                case IDictionary dictionary:
                {
                    return dictionary.GetEnumerator();
                }
                default:
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return null;
                }
            }
        }

        /// <summary>
        /// Removes the specified key from the dictionary.
        /// </summary>
        public void Remove(object key) => Remove((TKey)key);

        /// <summary>
        /// Adds a value to the list, growing it to a dictionary if necessary
        /// </summary>
        private void AddToOrUpdateList(List<KeyValuePair<TKey, TValue>> list, TKey key, TValue value, bool throwIfPresent)
        {
            if (list.Count < MaxListSize) // POLICY: Threshold balancing lookup time vs. space
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (comparer.Equals(list[i].Key, key))
                    {
                        if (throwIfPresent)
                        {
                            throw new ArgumentException("A value with the same key is already in the collection.");
                        }

                        list[i] = new KeyValuePair<TKey, TValue>(key, value);
                        return;
                    }
                }

                list.Add(new KeyValuePair<TKey, TValue>(key, value));
            }
            else
            {
                var newDictionary = new Dictionary<TKey, TValue>(list.Count + 1, comparer); // POLICY: Don't aggressively encourage extra capacity
                foreach (KeyValuePair<TKey, TValue> entry in list)
                {
                    newDictionary.Add(entry.Key, entry.Value);
                }

                if (throwIfPresent)
                {
                    newDictionary.Add(key, value);
                }
                else
                {
                    newDictionary[key] = value;
                }

                store = newDictionary;
            }
        }

        /// <summary>
        /// An enumerator for when the dictionary has only a single entry in it.
        /// </summary>
        private struct SingleEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// The single value.
            /// </summary>
            private KeyValuePair<TKey, TValue> value;

            /// <summary>
            /// Flag indicating when we are at the end of the enumeration.
            /// </summary>
            private bool enumerationComplete;

            /// <summary>
            /// Constructor.
            /// </summary>
            public SingleEnumerator(KeyValuePair<TKey, TValue> value)
            {
                this.value = value;
                enumerationComplete = false;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    if (enumerationComplete)
                    {
                        return value;
                    }

                    throw new InvalidOperationException("Past end of enumeration");
                }
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            object IEnumerator.Current
            {
                get { return ((IEnumerator<KeyValuePair<TKey, TValue>>)this).Current; }
            }

            /// <summary>
            /// Disposer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            public bool MoveNext()
            {
                if (!enumerationComplete)
                {
                    enumerationComplete = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Resets the enumerator.
            /// </summary>
            public void Reset()
            {
                enumerationComplete = false;
            }
        }

        /// <summary>
        /// An enumerator for when the dictionary has only a single entry in it.
        /// Cannot find a way to make the SingleEntryEnumerator serve both purposes, as foreach preferentially
        /// casts to IEnumerable that returns the generic enumerator instead of an IDictionaryEnumerator.
        /// 
        /// Don't want to use the List enumerator below as a throwaway one-entry list would need to be allocated.
        /// </summary>
        private struct SingleDictionaryEntryEnumerator : IDictionaryEnumerator
        {
            /// <summary>
            /// The single value.
            /// </summary>
            private DictionaryEntry value;

            /// <summary>
            /// Flag indicating when we are at the end of the enumeration.
            /// </summary>
            private bool enumerationComplete;

            /// <summary>
            /// Constructor.
            /// </summary>
            public SingleDictionaryEntryEnumerator(DictionaryEntry value)
            {
                this.value = value;
                enumerationComplete = false;
            }

            /// <summary>
            /// Key
            /// </summary>
            public object Key
            {
                get { return Entry.Key; }
            }

            /// <summary>
            /// Value
            /// </summary>
            public object Value
            {
                get { return Entry.Value; }
            }

            /// <summary>
            /// Current
            /// </summary>
            public object Current
            {
                get { return Entry; }
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public DictionaryEntry Entry
            {
                get
                {
                    if (enumerationComplete)
                    {
                        return value;
                    }

                    throw new InvalidOperationException("Past end of enumeration");
                }
            }

            /// <summary>
            /// Disposer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            public bool MoveNext()
            {
                if (!enumerationComplete)
                {
                    enumerationComplete = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Resets the enumerator.
            /// </summary>
            public void Reset()
            {
                enumerationComplete = false;
            }
        }

        /// <summary>
        /// An enumerator for a list of KVP that implements IDictionaryEnumerator
        /// </summary>
        /// <typeparam name="KK">Key type</typeparam>
        /// <typeparam name="VV">Value type</typeparam>
        private struct ListDictionaryEntryEnumerator<KK, VV> : IDictionaryEnumerator
        {
            /// <summary>
            /// The value.
            /// </summary>
            private IEnumerator<KeyValuePair<KK, VV>> enumerator;

            /// <summary>
            /// Constructor.
            /// </summary>
            public ListDictionaryEntryEnumerator(List<KeyValuePair<KK, VV>> list)
            {
                enumerator = list.GetEnumerator();
            }

            /// <summary>
            /// Key
            /// </summary>
            public object Key
            {
                get { return enumerator.Current.Key; }
            }

            /// <summary>
            /// Value
            /// </summary>
            public object Value
            {
                get { return enumerator.Current.Value; }
            }

            /// <summary>
            /// Current
            /// </summary>
            public object Current
            {
                get { return Entry; }
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public DictionaryEntry Entry
            {
                get { return new DictionaryEntry(enumerator.Current.Key, enumerator.Current.Value); }
            }

            /// <summary>
            /// Disposer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            public bool MoveNext()
            {
                return enumerator.MoveNext();
            }

            /// <summary>
            /// Resets the enumerator.
            /// </summary>
            public void Reset()
            {
                enumerator.Reset();
            }
        }
    }
}
