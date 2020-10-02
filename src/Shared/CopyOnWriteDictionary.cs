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
    internal class CopyOnWriteDictionary<V> : IDictionary<string, V>, IDictionary, ISerializable
    {
#if !NET35 // MSBuildNameIgnoreCaseComparer not compiled into MSBuildTaskHost but also allocations not interesting there.
        /// <summary>
        /// Empty dictionary with a <see cref="MSBuildNameIgnoreCaseComparer" />,
        /// used as the basis of new dictionaries with that comparer to avoid
        /// allocating new comparers objects.
        /// </summary>
        private readonly static ImmutableDictionary<string, V> NameComparerDictionaryPrototype = ImmutableDictionary.Create<string, V>((IEqualityComparer<string>)MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// Empty dictionary with <see cref="StringComparer.OrdinalIgnoreCase" />,
        /// used as the basis of new dictionaries with that comparer to avoid
        /// allocating new comparers objects.
        /// </summary>
        private readonly static ImmutableDictionary<string, V> OrdinalIgnoreCaseComparerDictionaryPrototype = ImmutableDictionary.Create<string, V>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
#endif


        /// <summary>
        /// The backing dictionary.
        /// Lazily created.
        /// </summary>
        private ImmutableDictionary<string, V> _backing;

        /// <summary>
        /// Constructor. Consider supplying a comparer instead.
        /// </summary>
        internal CopyOnWriteDictionary()
        {
            _backing = ImmutableDictionary<string, V>.Empty;
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
        internal CopyOnWriteDictionary(IEqualityComparer<string> keyComparer)
            : this(0, keyComparer)
        {
        }

        /// <summary>
        /// Constructor taking a specified comparer for the keys and an initial capacity
        /// </summary>
        internal CopyOnWriteDictionary(int capacity, IEqualityComparer<string>? keyComparer)
        {
            _backing = GetInitialDictionary(keyComparer);
        }

        /// <summary>
        /// Serialization constructor, for crossing appdomain boundaries
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "context", Justification = "Not needed")]
        protected CopyOnWriteDictionary(SerializationInfo info, StreamingContext context)
        {
            object v = info.GetValue(nameof(_backing), typeof(KeyValuePair<string, V>[]));

            object comparer = info.GetValue(nameof(Comparer), typeof(IEqualityComparer<string>));

            var b = GetInitialDictionary((IEqualityComparer<string>)comparer);

            _backing = b.AddRange((KeyValuePair<string, V>[])v);
        }

        private static ImmutableDictionary<string, V> GetInitialDictionary(IEqualityComparer<string>? keyComparer)
        {
#if NET35
            return ImmutableDictionary.Create<string, V>(keyComparer);
#else
            return keyComparer is MSBuildNameIgnoreCaseComparer
                            ? NameComparerDictionaryPrototype
                            : keyComparer == StringComparer.OrdinalIgnoreCase
                              ? OrdinalIgnoreCaseComparerDictionaryPrototype
                              : ImmutableDictionary.Create<string, V>(keyComparer);
#endif
        }

        /// <summary>
        /// Cloning constructor. Defers the actual clone.
        /// </summary>
        private CopyOnWriteDictionary(CopyOnWriteDictionary<V> that)
        {
            _backing = that._backing;
        }

        public CopyOnWriteDictionary(IDictionary<string, V> dictionary)
        {
            _backing = dictionary.ToImmutableDictionary();
        }

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<string> Keys => ((IDictionary<string, V>)_backing).Keys;

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<V> Values => ((IDictionary<string, V>)_backing).Values;

        /// <summary>
        /// Returns the number of items in the collection.
        /// </summary>
        public int Count => _backing.Count;

        /// <summary>
        /// Returns true if the collection is read-only.
        /// </summary>
        public bool IsReadOnly => ((IDictionary<string, V>)_backing).IsReadOnly;

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
        internal IEqualityComparer<string> Comparer
        {
            get => _backing.KeyComparer;
            private set => _backing = _backing.WithComparers(keyComparer: value);
        }

        /// <summary>
        /// Accesses the value for the specified key.
        /// </summary>
        public V this[string key]
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
                TryGetValue((string) key, out V val);
                return val;
            }

            set => this[(string)key] = (V)value;
        }
#nullable restore

        /// <summary>
        /// Adds a value to the dictionary.
        /// </summary>
        public void Add(string key, V value)
        {
            _backing = _backing.SetItem(key, value);
        }

        /// <summary>
        /// Returns true if the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return _backing.ContainsKey(key);
        }

        /// <summary>
        /// Removes the entry for the specified key from the dictionary.
        /// </summary>
        public bool Remove(string key)
        {
            ImmutableDictionary<string, V> initial = _backing;

            _backing = _backing.Remove(key);

            return initial != _backing; // whether the removal occured
        }

        /// <summary>
        /// Attempts to find the value for the specified key in the dictionary.
        /// </summary>
        public bool TryGetValue(string key, out V value)
        {
            return _backing.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        public void Add(KeyValuePair<string, V> item)
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
        public bool Contains(KeyValuePair<string, V> item)
        {
            return _backing.Contains(item);
        }

        /// <summary>
        /// Copies all of the elements of the collection to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<string, V>[] array, int arrayIndex)
        {
            ((IDictionary<string, V>)_backing).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Remove an item from the dictionary.
        /// </summary>
        public bool Remove(KeyValuePair<string, V> item)
        {
            ImmutableDictionary<string, V> initial = _backing;

            _backing = _backing.Remove(item.Key);

            return initial != _backing; // whether the removal occured
        }

        /// <summary>
        /// Implementation of generic IEnumerable.GetEnumerator()
        /// </summary>
        public IEnumerator<KeyValuePair<string, V>> GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        /// <summary>
        /// Implementation of IEnumerable.GetEnumerator()
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, V>>)this).GetEnumerator();
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void IDictionary.Add(object key, object value)
        {
            Add((string)key, (V)value);
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
            return ContainsKey((string)key);
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
            Remove((string)key);
        }

        /// <summary>
        /// IDictionary implementation.
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        {
            int i = 0;
            foreach (KeyValuePair<string, V> entry in this)
            {
                array.SetValue(new DictionaryEntry(entry.Key, entry.Value), index + i);
                i++;
            }
        }

        /// <summary>
        /// Clone, with the actual clone deferred
        /// </summary>
        internal CopyOnWriteDictionary<V> Clone()
        {
            return new CopyOnWriteDictionary<V>(this);
        }

        /// <summary>
        /// Returns true if these dictionaries have the same backing.
        /// </summary>
        internal bool HasSameBacking(CopyOnWriteDictionary<V> other)
        {
            return ReferenceEquals(other._backing, _backing);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ImmutableDictionary<string, V> snapshot = _backing;
            KeyValuePair<string, V>[] array = snapshot.ToArray();

            info.AddValue(nameof(_backing), array);
            info.AddValue(nameof(Comparer), Comparer);
        }
    }
}
