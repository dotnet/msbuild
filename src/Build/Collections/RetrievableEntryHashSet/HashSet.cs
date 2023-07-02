// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.Build.Shared;

// Difficult to make this nullable clean because although it doesn't accept null values,
// so IDictionary<string, T> is appropriate, Get() may return them. 
#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    ///    A dictionary for entries that know their own keys.
    ///    This is the standard Hashset with the following changes:
    ///
    ///    * require T implements IKeyed, and accept IKeyed directly where necessary
    ///    * all constructors require a comparer -- an IEqualityComparer&lt;IKeyed&gt; -- to avoid mistakes
    ///    * Get() to give you back the found entry, rather than just Contains() for a boolean
    ///    * Add() always adds, even if there's an entry already present with the same name (replacement semantics)
    ///    * Can set to read-only.
    ///    * implement IDictionary&lt;string, T&gt;
    ///    * some convenience methods taking 'string' as overloads of methods taking IKeyed.
    /// </summary>
    /// <typeparam name="T">The type of the thing, such as a Property.</typeparam>
    [DebuggerTypeProxy(typeof(ICollectionDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    internal class RetrievableEntryHashSet<T> : ICollection<T>,
        ISerializable, IDeserializationCallback,
        IDictionary<string, T>
        where T : class, IKeyed
    {
        // This uses the same array-based implementation as Dictionary<TKey, TValue>.

        // Constants for serialization
        private const string CapacityName = "Capacity"; // Do not rename (binary serialization)
        private const string ElementsName = "Elements"; // Do not rename (binary serialization)
        private const string ComparerName = "Comparer"; // Do not rename (binary serialization)
        private const string VersionName = "Version"; // Do not rename (binary serialization)

        /// <summary>
        /// When constructing a hashset from an existing collection, it may contain duplicates,
        /// so this is used as the max acceptable excess ratio of capacity to count. Note that
        /// this is only used on the ctor and not to automatically shrink if the hashset has, e.g,
        /// a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess.
        /// This is set to 3 because capacity is acceptable as 2x rounded up to nearest prime.
        /// </summary>
        private const int ShrinkThreshold = 3;
        private const int StartOfFreeList = -3;

        private int[] _buckets;
        private Entry[] _entries;
        private ulong _fastModMultiplier;
        private int _count;
        private int _freeList;
        private int _freeCount;
        private int _version;
        private IEqualityComparer<string> _comparer;
        private bool _readOnly;

        /// <summary>
        /// Dictionary for entries that contain their own keys.
        /// </summary>
        public RetrievableEntryHashSet(IEqualityComparer<string> comparer)
        {
            ErrorUtilities.VerifyThrowInternalError(comparer != null, "use explicit comparer");

            _comparer = comparer;
        }

        /// <summary>
        /// Dictionary for entries that contain their own keys.
        /// </summary>
        public RetrievableEntryHashSet(int suggestedCapacity, IEqualityComparer<string> comparer)
            : this(comparer) => Initialize(suggestedCapacity);

        /// <summary>
        /// Dictionary for entries that contain their own keys.
        /// </summary>
        public RetrievableEntryHashSet(IEnumerable<T> collection, IEqualityComparer<string> comparer)
            : this(comparer)
        {
            ErrorUtilities.VerifyThrowArgumentNull(collection, nameof(collection));

            if (collection is RetrievableEntryHashSet<T> otherAsHashSet && _comparer == otherAsHashSet._comparer)
            {
                ConstructFrom(otherAsHashSet);
            }
            else
            {
                if (collection is ICollection<T> coll)
                {
                    int count = coll.Count;
                    if (count > 0)
                    {
                        Initialize(count);
                    }
                }

                foreach (T item in collection)
                {
                    AddOrReplace(item);
                }

                if (_count > 0 && _entries.Length / _count > ShrinkThreshold)
                {
                    TrimExcess();
                }
            }
        }

        protected RetrievableEntryHashSet(SerializationInfo info, StreamingContext context) =>
            // We can't do anything with the keys and values until the entire graph has been 
            // deserialized and we have a reasonable estimate that GetHashCode is not going to 
            // fail.  For the time being, we'll just cache this.  The graph is not valid until 
            // OnDeserialization has been called.
            HashHelpers.SerializationInfoTable.Add(this, info);

        public int Count => _count - _freeCount;

        public bool IsReadOnly => _readOnly;

        public ICollection<string> Keys
        {
            get
            {
                string[] keys = new string[_count];

                int i = 0;
                foreach (T item in this)
                {
                    keys[i] = item.Key;
                    i++;
                }

                return keys;
            }
        }

        public ICollection<T> Values => this;

        T IDictionary<string, T>.this[string name]
        {
            get => this[name];
            set => this[name] = value;
        }

        internal T this[string name]
        {
            get => Get(name);
            set
            {
                Debug.Assert(string.Equals(name, value.Key, StringComparison.Ordinal));
                AddOrReplace(value);
            }
        }

        void ICollection<T>.Add(T item) => AddOrReplace(item);

        /// <summary>
        /// Remove all items from this set. This clears the elements but not the underlying 
        /// buckets and slots array. You may follow this call by TrimExcess to release these.
        /// </summary>
        public void Clear()
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!_readOnly, "OM_NotSupportedReadOnlyCollection");

            int count = _count;
            if (count > 0)
            {
                Array.Clear(_buckets, 0, _buckets.Length);
                _count = 0;
                _freeList = -1;
                _freeCount = 0;
                Array.Clear(_entries, 0, count);
            }
        }

        /// <summary>Determines whether the <see cref="HashSet{T}"/> contains the specified element.</summary>
        /// <param name="item">The element to locate in the <see cref="HashSet{T}"/> object.</param>
        /// <returns>true if the <see cref="HashSet{T}"/> object contains the specified element; otherwise, false.</returns>
        public bool Contains(T item) => Get(item.Key) != null;

        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> entry)
        {
            Debug.Assert(string.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Get(entry.Value.Key) != null;
        }

        public bool ContainsKey(string key) => Get(key) != null;

        public bool TryGetValue(string key, out T item)
        {
            item = Get(key);
            return item != null;
        }

        /// <summary>
        /// Gets the item if any with the given name.
        /// </summary>
        /// <param name="key">key to check for containment</param>
        /// <returns>item if found, otherwise null</returns>
        public T Get(string key) => GetCore(key, 0, key.Length);

        /// <summary>
        /// Gets the item if any with the given name.
        /// </summary>
        /// <param name="key">key to check for containment</param>
        /// <param name="index">The position of the substring within <paramref name="key"/>.</param>
        /// <param name="length">The maximum number of characters in the <paramref name="key"/> to lookup.</param>
        /// <returns>item if found, otherwise null</returns>
        public T Get(string key, int index, int length)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(length < 0, nameof(length));
            ErrorUtilities.VerifyThrowArgumentOutOfRange(index >= 0 && index <= key.Length - length, nameof(index));

            return GetCore(key, index, length);
        }

        /// <summary>Initializes the HashSet from another HashSet with the same element type and equality comparer.</summary>
        private void ConstructFrom(RetrievableEntryHashSet<T> source)
        {
            if (source.Count == 0)
            {
                return;
            }

            int capacity = source._buckets.Length;
            int threshold = HashHelpers.ExpandPrime(source.Count + 1);

            if (threshold >= capacity)
            {
                _buckets = (int[])source._buckets.Clone();
                _entries = (Entry[])source._entries.Clone();
                _freeList = source._freeList;
                _freeCount = source._freeCount;
                _count = source._count;
                _fastModMultiplier = source._fastModMultiplier;
            }
            else // source had too much slack capacity
            {
                Initialize(source.Count);

                Entry[] entries = source._entries;
                for (int i = 0; i < source._count; i++)
                {
                    ref Entry entry = ref entries[i];
                    if (entry.Next >= -1)
                    {
                        AddOrReplace(entry.Value);
                    }
                }
            }

            _readOnly = source._readOnly;

            Debug.Assert(Count == source.Count);
        }

        /// <summary>
        /// Gets the item if any with the given name
        /// </summary>
        /// <param name="item">item to check for containment</param>
        /// <param name="index">The position of the substring within <paramref name="item"/>.</param>
        /// <param name="length">The maximum number of characters in the <paramref name="item"/> to lookup.</param>
        /// <returns>item if found, otherwise null</returns>
        private T GetCore(string item, int index, int length)
        {
            Entry[] entries = _entries;
            if (_entries == null)
            {
                return default;
            }

            uint collisionCount = 0;
            IEqualityComparer<string> comparer = _comparer;
            IConstrainedEqualityComparer<string> constrainedComparer = null;
            int hashCode = 0;
            if (index != 0 || length != item.Length)
            {
                constrainedComparer = comparer as IConstrainedEqualityComparer<string>;
                Debug.Assert(constrainedComparer != null, "need constrained comparer to compare with index/length");
                hashCode = constrainedComparer.GetHashCode(item, index, length);
            }
            else
            {
                hashCode = comparer.GetHashCode(item);
            }

            int i = GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
            while (i >= 0)
            {
                ref Entry entry = ref entries[i];
                if (entry.HashCode == hashCode &&
                    constrainedComparer == null ? comparer.Equals(entry.Value.Key, item) : constrainedComparer.Equals(entry.Value.Key, item, index, length))
                {
                    return entry.Value;
                }

                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                {
                    // The chain of entries forms a loop, which means a concurrent update has happened.
                    ErrorUtilities.ThrowInternalError("corrupted");
                }
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref int GetBucketRef(int hashCode)
        {
            int[] buckets = _buckets;
            return ref buckets[HashHelpers.FastMod((uint)hashCode, (uint)buckets.Length, _fastModMultiplier)];
        }

        public bool Remove(T item) => Remove(item.Key);

        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> entry)
        {
            Debug.Assert(string.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Remove(entry.Value);
        }

        public bool Remove(string item)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!_readOnly, "OM_NotSupportedReadOnlyCollection");

            Entry[] entries = _entries;
            if (_entries == null)
            {
                return default;
            }

            uint collisionCount = 0;
            int last = -1;

            int hashCode = _comparer.GetHashCode(item);

            ref int bucket = ref GetBucketRef(hashCode);
            int i = bucket - 1; // Value in buckets is 1-based

            while (i >= 0)
            {
                ref Entry entry = ref entries[i];

                if (entry.HashCode == hashCode && _comparer.Equals(entry.Value.Key, item))
                {
                    if (last < 0)
                    {
                        bucket = entry.Next + 1; // Value in buckets is 1-based
                    }
                    else
                    {
                        entries[last].Next = entry.Next;
                    }

                    Debug.Assert((StartOfFreeList - _freeList) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
                    entry.Next = StartOfFreeList - _freeList;
                    entry.Value = default!;

                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    ErrorUtilities.ThrowInternalError("corrupted");
                }
            }

            return false;
        }

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            foreach (T entry in this)
            {
                yield return new KeyValuePair<string, T>(entry.Key, entry);
            }
        }

        /// <summary>
        /// Permanently prevent changes to the set.
        /// </summary>
        internal void MakeReadOnly() => _readOnly = true;

        #region Serialization

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new InvalidOperationException();
            }

            info.AddValue(VersionName, _version); // need to serialize version to avoid problems with serializing while enumerating
            info.AddValue(ComparerName, _comparer, typeof(IEqualityComparer<string>));
            info.AddValue(CapacityName, _buckets == null ? 0 : _buckets.Length);

            if (_buckets != null)
            {
                var array = new T[Count];
                CopyTo(array);
                info.AddValue(ElementsName, array, typeof(T[]));
            }
        }

        public virtual void OnDeserialization(object sender)
        {
            _ = HashHelpers.SerializationInfoTable.TryGetValue(this, out SerializationInfo siInfo);
            if (siInfo == null)
            {
                // It might be necessary to call OnDeserialization from a container if the 
                // container object also implements OnDeserialization. However, remoting will 
                // call OnDeserialization again. We can return immediately if this function is 
                // called twice. Note we set m_siInfo to null at the end of this method.
                return;
            }

            int capacity = siInfo.GetInt32(CapacityName);
            _comparer = (IEqualityComparer<string>)siInfo.GetValue(ComparerName, typeof(IEqualityComparer<string>))!;
            _freeList = -1;
            _freeCount = 0;

            if (capacity != 0)
            {
                _buckets = new int[capacity];
                _entries = new Entry[capacity];
                _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)capacity);

                T[] array = (T[])siInfo.GetValue(ElementsName, typeof(T[])) ?? throw new InvalidOperationException();

                // there are no resizes here because we already set capacity above
                for (int i = 0; i < array.Length; i++)
                {
                    AddOrReplace(array[i]);
                }
            }
            else
            {
                _buckets = null;
            }

            _version = siInfo.GetInt32(VersionName);
            _ = HashHelpers.SerializationInfoTable.Remove(this);
        }

        #endregion

        void IDictionary<string, T>.Add(string key, T item)
        {
            if (key != item.Key)
            {
                throw new InvalidOperationException();
            }

            AddOrReplace(item);
        }

        void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> entry)
        {
            Debug.Assert(string.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));

            AddOrReplace(entry.Value);
        }

        // Copy all elements into array starting at zero based index specified
        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int index)
        {
            int i = index;
            foreach (T entry in this)
            {
                array[i] = new KeyValuePair<string, T>(entry.Key, entry);
                i++;
            }
        }

        /// <summary>Copies the elements of a <see cref="HashSet{T}"/> object to an array, starting at the specified array index.</summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex) => CopyTo(array, arrayIndex, Count);

        private void CopyTo(T[] array) => CopyTo(array, 0, Count);

        private void CopyTo(T[] array, int arrayIndex, int count)
        {
            ErrorUtilities.VerifyThrowArgumentNull(array, nameof(array));
            ErrorUtilities.VerifyThrowArgumentOutOfRange(arrayIndex >= 0, nameof(arrayIndex));
            ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0, nameof(count));
            ErrorUtilities.VerifyThrowArgument(arrayIndex < array.Length && count <= array.Length - arrayIndex, "ArrayPlusOffTooSmall");

            Entry[] entries = _entries;
            for (int i = 0; i < _count && count != 0; i++)
            {
                ref Entry entry = ref entries[i];
                if (entry.Next >= -1)
                {
                    array[arrayIndex++] = entry.Value;
                    count--;
                }
            }
        }

        /// <summary>
        /// Sets the capacity of a <see cref="HashSet{T}"/> object to the actual number of elements it contains,
        /// rounded up to a nearby, implementation-specific value.
        /// </summary>
        public void TrimExcess()
        {
            int capacity = Count;

            int newSize = HashHelpers.GetPrime(capacity);
            Entry[] oldEntries = _entries;
            int currentCapacity = oldEntries == null ? 0 : oldEntries.Length;
            if (newSize >= currentCapacity)
            {
                return;
            }

            int oldCount = _count;
            _version++;
            Initialize(newSize);
            Entry[] entries = _entries;
            int count = 0;
            for (int i = 0; i < oldCount; i++)
            {
                int hashCode = oldEntries[i].HashCode; // At this point, we know we have entries.
                if (oldEntries[i].Next >= -1)
                {
                    ref Entry entry = ref entries[count];
                    entry = oldEntries[i];
                    ref int bucket = ref GetBucketRef(hashCode);
                    entry.Next = bucket - 1; // Value in _buckets is 1-based
                    bucket = count + 1;
                    count++;
                }
            }

            _count = capacity;
            _freeCount = 0;
        }

        /// <summary>
        /// Adds value to HashSet even if already present.
        /// </summary>
        /// <param name="value">value to add</param>
        public void AddOrReplace(T value)
        {
            ErrorUtilities.VerifyThrowArgumentNull(value, nameof(value));
            ErrorUtilities.VerifyThrowInvalidOperation(!_readOnly, "OM_NotSupportedReadOnlyCollection");

            Entry[] entries = _entries;
            if (entries == null)
            {
                Initialize(0);
                entries = _entries;
            }

            IEqualityComparer<string> comparer = _comparer;

            uint collisionCount = 0;
            ref int bucket = ref Unsafe.NullRef<int>();

            string key = value.Key;
            int hashCode = (value == null) ? 0 : _comparer.GetHashCode(value.Key);
            bucket = ref GetBucketRef(hashCode);
            int i = bucket - 1; // Value in _buckets is 1-based
            while (i >= 0)
            {
                ref Entry entry = ref entries[i];
                if (entry.HashCode == hashCode && comparer.Equals(entry.Value.Key, key))
                {
                    // matches -- replace it
                    entries[i].Value = value;
                    return;
                }

                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                {
                    // The chain of entries forms a loop, which means a concurrent update has happened.
                    ErrorUtilities.ThrowInternalError("corrupted");
                }
            }

            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                _freeCount--;
                Debug.Assert((StartOfFreeList - entries[_freeList].Next) >= -1, "shouldn't overflow because `next` cannot underflow");
                _freeList = StartOfFreeList - entries[_freeList].Next;
            }
            else
            {
                int count = _count;
                if (count == entries.Length)
                {
                    Resize();
                    bucket = ref GetBucketRef(hashCode);
                }

                index = count;
                _count = count + 1;
                entries = _entries;
            }

            {
                ref Entry entry = ref entries[index];
                entry.HashCode = hashCode;
                entry.Next = bucket - 1; // Value in _buckets is 1-based
                entry.Value = value;
                bucket = index + 1;
                _version++;
            }

            return;
        }

        /// <summary>
        /// Equality comparison against another of this type.
        /// Compares entries by reference - not merely by using the comparison on the key.
        /// </summary>
        internal bool EntriesAreReferenceEquals(RetrievableEntryHashSet<T> other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Count != other.Count)
            {
                return false;
            }

            foreach (T element in other)
            {
                if (!TryGetValue(element.Key, out T ours) || !ReferenceEquals(element, ours))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
        /// greater than or equal to capacity.
        /// </summary>
        private void Initialize(int capacity)
        {
            int size = HashHelpers.GetPrime(capacity);
            int[] buckets = new int[size];
            var entries = new Entry[size];

            _freeList = -1;
            _buckets = buckets;
            _entries = entries;
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);
        }

        private void Resize()
        {
            int newSize = HashHelpers.ExpandPrime(_count);

            Debug.Assert(_entries != null, "_entries should be non-null");
            Debug.Assert(newSize >= _entries.Length);

            var entries = new Entry[newSize];

            int count = _count;
            Array.Copy(_entries, entries, count);

            // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
            _buckets = new int[newSize];
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);

            for (int i = 0; i < count; i++)
            {
                ref Entry entry = ref entries[i];
                if (entry.Next >= -1)
                {
                    ref int bucket = ref GetBucketRef(entry.HashCode);
                    entry.Next = bucket - 1; // Value in _buckets is 1-based
                    bucket = i + 1;
                }
            }

            _entries = entries;
        }

        private struct Entry
        {
            public int HashCode;

            /// <summary>
            /// 0-based index of next entry in chain: -1 means end of chain
            /// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
            /// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
            /// </summary>
            public int Next;
            public T Value;
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly RetrievableEntryHashSet<T> _hashSet;
            private readonly int _version;
            private int _index;

            internal Enumerator(RetrievableEntryHashSet<T> hashSet)
            {
                _hashSet = hashSet;
                _version = hashSet._version;
                _index = 0;
                Current = default;
            }

            public T Current { get; private set; }

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _hashSet._count + 1))
                    {
                        throw new InvalidOperationException();
                    }

                    return Current;
                }
            }

            public bool MoveNext()
            {
                if (_version != _hashSet._version)
                {
                    throw new InvalidOperationException();
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is int.MaxValue
                while ((uint)_index < (uint)_hashSet._count)
                {
                    ref Entry entry = ref _hashSet._entries[_index++];
                    if (entry.Next >= -1)
                    {
                        Current = entry.Value;
                        return true;
                    }
                }

                _index = _hashSet._count + 1;
                Current = default;
                return false;
            }

            public void Dispose() { }

            void IEnumerator.Reset()
            {
                if (_version != _hashSet._version)
                {
                    throw new InvalidOperationException();
                }

                _index = 0;
                Current = default!;
            }
        }
    }
}
