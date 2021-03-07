// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Lightweight, read-only IDictionary implementation using two arrays
    /// and O(n) lookup.
    /// Requires specifying capacity at construction and does not
    /// support reallocation to increase capacity.
    /// </summary>
    /// <typeparam name="TKey">Type of keys</typeparam>
    /// <typeparam name="TValue">Type of values</typeparam>
    internal class ArrayDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>
    {
        private TKey[] keys;
        private TValue[] values;

        private int count;

        public ArrayDictionary(int capacity)
        {
            keys = new TKey[capacity];
            values = new TValue[capacity];
        }

        public static IDictionary<TKey, TValue> Create(int capacity)
        {
            return new ArrayDictionary<TKey, TValue>(capacity);
        }

        public TValue this[TKey key]
        {
            get
            {
                TryGetValue(key, out var value);
                return value;
            }

            set
            {
                var comparer = KeyComparer;
                for (int i = 0; i < count; i++)
                {
                    if (comparer.Equals(key, keys[i]))
                    {
                        values[i] = value;
                        return;
                    }
                }

                Add(key, value);
            }
        }

        object IDictionary.this[object key]
        {
            get => this[(TKey)key];
            set => this[(TKey)key] = (TValue)value;
        }

        public ICollection<TKey> Keys => keys;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => values;

        ICollection IDictionary.Keys => keys;

        public ICollection<TValue> Values => values;

        ICollection IDictionary.Values => values;

        private IEqualityComparer<TKey> KeyComparer => EqualityComparer<TKey>.Default;

        private IEqualityComparer<TValue> ValueComparer => EqualityComparer<TValue>.Default;

        public int Count => count;

        public bool IsReadOnly => true;

        bool IDictionary.IsFixedSize => true;

        object ICollection.SyncRoot => this;

        bool ICollection.IsSynchronized => false;

        public void Add(TKey key, TValue value)
        {
            if (count < keys.Length)
            {
                keys[count] = key;
                values[count] = value;
                count += 1;
            }
            else
            {
                throw new InvalidOperationException($"ArrayDictionary is at capacity {keys.Length}");
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            var keyComparer = KeyComparer;
            var valueComparer = ValueComparer;
            for (int i = 0; i < count; i++)
            {
                if (keyComparer.Equals(item.Key, keys[i]) && valueComparer.Equals(item.Value, values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsKey(TKey key)
        {
            var comparer = KeyComparer;
            for (int i = 0; i < count; i++)
            {
                if (comparer.Equals(key, keys[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (int i = 0; i < count; i++)
            {
                array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new Enumerator(this, emitDictionaryEntries: true);
        }

        public bool Remove(TKey key)
        {
            throw new System.NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new System.NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var comparer = KeyComparer;
            for (int i = 0; i < count; i++)
            {
                if (comparer.Equals(key, keys[i]))
                {
                    value = values[i];
                    return true;
                }
            }

            value = default;
            return false;
        }

        bool IDictionary.Contains(object key)
        {
            if (key is not TKey typedKey)
            {
                return false;
            }

            return ContainsKey(typedKey);
        }

        void IDictionary.Add(object key, object value)
        {
            if (key is TKey typedKey && value is TValue typedValue)
            {
                Add(typedKey, typedValue);
            }

            throw new NotSupportedException();
        }

        void IDictionary.Remove(object key)
        {
            throw new NotImplementedException();
        }

        private struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
        {
            private readonly ArrayDictionary<TKey, TValue> _dictionary;
            private readonly bool _emitDictionaryEntries;
            private int _position;

            public Enumerator(ArrayDictionary<TKey, TValue> dictionary, bool emitDictionaryEntries = false)
            {
                this._dictionary = dictionary;
                this._position = -1;
                this._emitDictionaryEntries = emitDictionaryEntries;
            }

            public KeyValuePair<TKey, TValue> Current =>
                new KeyValuePair<TKey, TValue>(
                    _dictionary.keys[_position],
                    _dictionary.values[_position]);

            private DictionaryEntry CurrentDictionaryEntry => new DictionaryEntry(_dictionary.keys[_position], _dictionary.values[_position]);

            object IEnumerator.Current => _emitDictionaryEntries ? CurrentDictionaryEntry : Current;

            object IDictionaryEnumerator.Key => _dictionary.keys[_position];

            object IDictionaryEnumerator.Value => _dictionary.values[_position];

            DictionaryEntry IDictionaryEnumerator.Entry => CurrentDictionaryEntry;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                _position += 1;
                return _position < _dictionary.Count;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}