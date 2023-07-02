// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

// Difficult to make this nullable clean because although it doesn't accept null values,
// so IDictionary<string, T> is appropriate, Get() may return them. 
#nullable disable

/*
    ==================================================================================================================

    This is the standard Hashset with the following changes:

    * class renamed
    * require T implements IKeyed, and accept IKeyed directly where necessary
    * all constructors require a comparer -- an IEqualityComparer<IKeyed> -- to avoid mistakes
    * change Contains to give you back the found entry, rather than a boolean
    * change Add so that it always adds, even if there's an entry already present with the same name. 
           We want "replacement" semantics, like a dictionary keyed on name.
    * constructor that allows the collection to be read-only
    * implement IDictionary<string, T>
    * some convenience methods taking 'string' as overloads of methods taking IKeyed

    ==================================================================================================================
*/

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Implementation notes:
    /// This uses an array-based implementation similar to <see cref="Dictionary{TKey, TValue}" />, using a buckets array
    /// to map hash values to the Slots array. Items in the Slots array that hash to the same value
    /// are chained together through the "next" indices. 
    /// 
    /// The capacity is always prime; so during resizing, the capacity is chosen as the next prime
    /// greater than double the last capacity. 
    /// 
    /// The underlying data structures are lazily initialized. Because of the observation that, 
    /// in practice, hashtables tend to contain only a few elements, the initial capacity is
    /// set very small (3 elements) unless the ctor with a collection is used.
    /// 
    /// The +/- 1 modifications in methods that add, check for containment, etc allow us to 
    /// distinguish a hash code of 0 from an uninitialized bucket. This saves us from having to 
    /// reset each bucket to -1 when resizing. See Contains, for example.
    /// 
    /// Set methods such as UnionWith, IntersectWith, ExceptWith, and SymmetricExceptWith modify
    /// this set.
    /// 
    /// Some operations can perform faster if we can assume "other" contains unique elements
    /// according to this equality comparer. The only times this is efficient to check is if
    /// other is a hashset. Note that checking that it's a hashset alone doesn't suffice; we
    /// also have to check that the hashset is using the same equality comparer. If other 
    /// has a different equality comparer, it will have unique elements according to its own
    /// equality comparer, but not necessarily according to ours. Therefore, to go these 
    /// optimized routes we check that other is a hashset using the same equality comparer.
    /// 
    /// A HashSet with no elements has the properties of the empty set. (See IsSubset, etc. for 
    /// special empty set checks.)
    /// 
    /// A couple of methods have a special case if other is this (e.g. SymmetricExceptWith). 
    /// If we didn't have these checks, we could be iterating over the set and modifying at
    /// the same time. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
#if TARGET_64BIT
        private ulong _fastModMultiplier;
#endif
        private int _count;
        private int _freeList;
        private int _freeCount;
        private int _version;
        private IEqualityComparer<string> _comparer;
        private IConstrainedEqualityComparer<string> _constrainedComparer;
        private bool _readOnly; // TODO -- needed?

        #region Constructors

        public RetrievableEntryHashSet(IEqualityComparer<string> comparer)
        {
            ErrorUtilities.VerifyThrowInternalError(comparer != null, "use explicit comparer");

            _comparer = comparer;
            _constrainedComparer = comparer as IConstrainedEqualityComparer<string>;
        }

        public RetrievableEntryHashSet(IEnumerable<T> collection, IEqualityComparer<string> comparer, bool readOnly = false)
            : this(collection, comparer)
        {
            _readOnly = true; // Set after possible initialization from another collection
        }

        public RetrievableEntryHashSet(IEnumerable<KeyValuePair<string, T>> collection, IEqualityComparer<string> comparer, bool readOnly = false)
            : this(collection.Values(), comparer, readOnly)
        {
            _readOnly = true; // Set after possible initialization from another collection
        }

        /// <summary>
        /// Implementation Notes:
        /// Since resizes are relatively expensive (require rehashing), this attempts to minimize 
        /// the need to resize by setting the initial capacity based on size of collection. 
        /// </summary>
        public RetrievableEntryHashSet(int suggestedCapacity, IEqualityComparer<string> comparer)
            : this(comparer)
        {
            Initialize(suggestedCapacity);
        }

        /// <summary>
        /// Implementation Notes:
        /// Since resizes are relatively expensive (require rehashing), this attempts to minimize 
        /// the need to resize by settingnull the initial capacity based on size of collection. 
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
                // to avoid excess resizes, first set size based on collection's count. Collection
                // may contain duplicates, so call TrimExcess if resulting hashset is larger than
                // threshold
                if (collection is ICollection<T> coll)
                {
                    int count = coll.Count;
                    if (count > 0)
                    {
                        Initialize(count);
                    }
                }

                UnionWith(collection);

                if (_count > 0 && _entries!.Length / _count > ShrinkThreshold)
                {
                    TrimExcess();
                }
            }
        }

        protected RetrievableEntryHashSet(SerializationInfo info, StreamingContext context)
        {
            // We can't do anything with the keys and values until the entire graph has been 
            // deserialized and we have a reasonable estimate that GetHashCode is not going to 
            // fail.  For the time being, we'll just cache this.  The graph is not valid until 
            // OnDeserialization has been called.
            HashHelpers.SerializationInfoTable.Add(this, info);
        }

        /// <summary>Initializes the HashSet from another HashSet with the same element type and equality comparer.</summary>
        private void ConstructFrom(RetrievableEntryHashSet<T> source)
        {
            if (source.Count == 0)
            {
                // As well as short-circuiting on the rest of the work done,
                // this avoids errors from trying to access source._buckets
                // or source._entries when they aren't initialized.
                return;
            }

            int capacity = source._buckets!.Length;
            int threshold = HashHelpers.ExpandPrime(source.Count + 1);

            if (threshold >= capacity)
            {
                _buckets = (int[])source._buckets.Clone();
                _entries = (Entry[])source._entries!.Clone();
                _freeList = source._freeList;
                _freeCount = source._freeCount;
                _count = source._count;
#if TARGET_64BIT
                _fastModMultiplier = source._fastModMultiplier;
#endif
            }
            else
            {
                Initialize(source.Count);

                Entry[] entries = source._entries;
                for (int i = 0; i < source._count; i++)
                {
                    ref Entry entry = ref entries![i];
                    if (entry.Next >= -1)
                    {
                        AddEvenIfPresent(entry.Value);
                    }
                }
            }

            _readOnly = source._readOnly;

            Debug.Assert(Count == source.Count);
        }

        #endregion

        // Convenience to minimise change to callers used to dictionaries
        public ICollection<string> Keys
        {
            get
            {
                var keys = new string[_count];

                int i = 0;
                foreach (var item in this)
                {
                    keys[i] = item.Key;
                    i++;
                }

                return keys;
            }
        }

        // Convenience to minimise change to callers used to dictionaries
        public ICollection<T> Values => this;

        #region ICollection<T> methods

        // Convenience to minimise change to callers used to dictionaries
        internal T this[string name]
        {
            get => Get(name);
            set
            {
                Debug.Assert(String.Equals(name, value.Key, StringComparison.Ordinal));
                Add(value);
            }
        }

        /// <summary>
        /// Add item to this hashset. This is the explicit implementation of the <see cref="ICollection{T}" />
        /// interface. The other Add method returns bool indicating whether item was added.
        /// </summary>
        /// <param name="item">item to add</param>
        void ICollection<T>.Add(T item) => AddEvenIfPresent(item);

        /// <summary>
        /// Remove all items from this set. This clears the elements but not the underlying 
        /// buckets and slots array. Follow this call by TrimExcess to release these.
        /// </summary>
        public void Clear()
        {
            if (_readOnly)
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            }

            int count = _count;
            if (count > 0)
            {
                Debug.Assert(_buckets != null, "_buckets should be non-null");
                Debug.Assert(_entries != null, "_entries should be non-null");

                Array.Clear(_buckets, 0, _buckets!.Length);
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

        // Convenience
        internal bool Contains(string key) => Get(key) != null;

        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> entry)
        {
            Debug.Assert(String.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Get(entry.Value.Key) != null;
        }

        public bool ContainsKey(string key) => Get(key) != null;

        T IDictionary<string, T>.this[string name]
        {
            get => Get(name);
            set => Add(value);
        }

        // Convenience to minimise change to callers used to dictionaries
        public bool TryGetValue(string key, out T item)
        {
            item = Get(key);
            return item != null;
        }

        /// <summary>
        /// Gets the item if any with the given name
        /// </summary>
        /// <param name="key">key to check for containment</param>
        /// <returns>item if found, otherwise null</returns>
        public T Get(string key) => GetCore(key, 0, key.Length);

        /// <summary>
        /// Gets the item if any with the given name
        /// </summary>
        /// <param name="key">key to check for containment</param>
        /// <param name="index">The position of the substring within <paramref name="key"/>.</param>
        /// <param name="length">The maximum number of characters in the <paramref name="key"/> to lookup.</param>
        /// <returns>item if found, otherwise null</returns>
        public T Get(string key, int index, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (index < 0 || index > key.Length - length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_constrainedComparer == null)
            {
                throw new InvalidOperationException("Cannot do a constrained lookup on this collection.");
            }

            return GetCore(key, index, length);
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
            int[] buckets = _buckets;
            if (buckets != null)
            {
                Entry[] entries = _entries;
                Debug.Assert(entries != null, "Expected _entries to be initialized");

                uint collisionCount = 0;
                IConstrainedEqualityComparer<string> comparer = _constrainedComparer;
                {
                    int hashCode = InternalGetHashCode(item, index, length);

                    int i = GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
                    while (i >= 0)
                    {
                        ref Entry entry = ref entries[i];
                        if (entry.HashCode == hashCode &&
                            _constrainedComparer != null ? _constrainedComparer.Equals(entry.Value.Key, item, index, length) : _comparer.Equals(entry.Value.Key, item))
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
                }
            }

            // either m_buckets is null or wasn't found
            return default;
        }

        /// <summary>Gets a reference to the specified hashcode's bucket, containing an index into <see cref="_entries"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref int GetBucketRef(int hashCode)
        {
            int[] buckets = _buckets!;
#if TARGET_64BIT
            return ref buckets[HashHelpers.FastMod((uint)hashCode, (uint)buckets.Length, _fastModMultiplier)];
#else
            return ref buckets[(uint)hashCode % (uint)buckets.Length];
#endif
        }

        /// <summary>
        /// Remove entry that compares equal to T
        /// </summary>        
        public bool Remove(T item) => Remove(item.Key);

        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> entry)
        {
            Debug.Assert(String.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Remove(entry.Value);
        }

        public bool Remove(string item)
        {
            if (_buckets != null)
            {
                Entry[] entries = _entries;
                Debug.Assert(entries != null, "entries should be non-null");

                uint collisionCount = 0;
                int last = -1;

                int hashCode = (item == null) ? 0 : _comparer.GetHashCode(item);

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
            }

            return false;
        }

        public int Count => _count - _freeCount;

        /// <summary>
        /// Whether this is readonly
        /// </summary>
        public bool IsReadOnly => _readOnly;


        /// <summary>
        /// Permanently prevent changes to the set.
        /// </summary>
        internal void MakeReadOnly() => _readOnly = true;

        #endregion

        #region IEnumerable methods

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            foreach (var entry in this)
            {
                yield return new KeyValuePair<string, T>(entry.Key, entry);
            }
        }

        #endregion

        #region ISerializable methods

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

        #endregion

        #region IDeserializationCallback methods

        public virtual void OnDeserialization(object sender)
        {
            HashHelpers.SerializationInfoTable.TryGetValue(this, out SerializationInfo siInfo);
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
            _constrainedComparer = _comparer as IConstrainedEqualityComparer<string>;
            _freeList = -1;
            _freeCount = 0;

            if (capacity != 0)
            {
                _buckets = new int[capacity];
                _entries = new Entry[capacity];
#if TARGET_64BIT
                _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)capacity);
#endif

                T[] array = (T[])siInfo.GetValue(ElementsName, typeof(T[]));

                if (array == null)
                {
                    throw new InvalidOperationException();
                }

                // there are no resizes here because we already set capacity above
                for (int i = 0; i < array.Length; i++)
                {
                    AddEvenIfPresent(array[i]);
                }
            }
            else
            {
                _buckets = null;
            }

            _version = siInfo.GetInt32(VersionName);
            HashHelpers.SerializationInfoTable.Remove(this);
        }

        #endregion

        #region HashSet methods

        /// <summary>
        /// Add item to this HashSet. 
        /// *** MSBUILD NOTE: Always added - overwrite semantics
        /// </summary>
        public void Add(T item) => AddEvenIfPresent(item);

        void IDictionary<string, T>.Add(string key, T item)
        {
            if (key != item.Key)
            {
                throw new InvalidOperationException();
            }

            AddEvenIfPresent(item);
        }

        void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> entry)
        {
            Debug.Assert(String.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));

            AddEvenIfPresent(entry.Value);
        }

        /// <summary>
        /// Take the union of this HashSet with other. Modifies this set.
        /// </summary>
        /// <param name="other">enumerable with items to add</param>
        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T item in other)
            {
                AddEvenIfPresent(item);
            }
        }

        // Copy all elements into array starting at zero based index specified
        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int index)
        {
            int i = index;
            foreach (var entry in this)
            {
                array[i] = new KeyValuePair<string, T>(entry.Key, entry);
                i++;
            }
        }

        public void CopyTo(T[] array) => CopyTo(array, 0, Count);

        /// <summary>Copies the elements of a <see cref="HashSet{T}"/> object to an array, starting at the specified array index.</summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex) => CopyTo(array, arrayIndex, Count);

        public void CopyTo(T[] array, int arrayIndex, int count)
        {
            ErrorUtilities.VerifyThrowArgumentNull(array, nameof(array));
            ErrorUtilities.VerifyThrowArgumentOutOfRange(arrayIndex >= 0, nameof(arrayIndex));
            ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0, nameof(count));

            // Will the array, starting at arrayIndex, be able to hold elements? Note: not
            // checking arrayIndex >= array.Length (consistency with list of allowing
            // count of 0; subsequent check takes care of the rest)
            ErrorUtilities.VerifyThrowArgument(arrayIndex < array.Length && count <= array.Length - arrayIndex, "ArrayPlusOffTooSmall");

            Entry[] entries = _entries;
            for (int i = 0; i < _count && count != 0; i++)
            {
                ref Entry entry = ref entries![i];
                if (entry.Next >= -1)
                {
                    array[arrayIndex++] = entry.Value;
                    count--;
                }
            }
        }

        private void Resize() => Resize(HashHelpers.ExpandPrime(_count), forceNewHashCodes: false);

        private void Resize(int newSize, bool forceNewHashCodes)
        {
            // Value types never rehash
            Debug.Assert(!forceNewHashCodes || !typeof(T).IsValueType);
            Debug.Assert(_entries != null, "_entries should be non-null");
            Debug.Assert(newSize >= _entries.Length);

            var entries = new Entry[newSize];

            int count = _count;
            Array.Copy(_entries, entries, count);

            // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
            _buckets = new int[newSize];
#if TARGET_64BIT
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);
#endif
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
                int hashCode = oldEntries![i].HashCode; // At this point, we know we have entries.
                if (oldEntries[i].Next >= -1)
                {
                    ref Entry entry = ref entries![count];
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

        #endregion

        #region Helper methods

        /// <summary>
        /// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
        /// greater than or equal to capacity.
        /// </summary>
        /// <param name="capacity"></param>
        private int Initialize(int capacity)
        {
            int size = HashHelpers.GetPrime(capacity);
            var buckets = new int[size];
            var entries = new Entry[size];


            _freeList = -1;
            _buckets = buckets;
            _entries = entries;
#if TARGET_64BIT
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);
#endif

            return size;
        }

        /// <summary>
        /// Adds value to HashSet if not contained already
        /// Returns true if added and false if already present
        /// ** MSBUILD: Modified so that it DOES add even if present. It will return false in that case, though.**
        /// </summary>
        /// <param name="value">value to find</param>
        /// <returns></returns>
        private bool AddEvenIfPresent(T value)
        {
            if (_readOnly)
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            }

            if (_buckets == null)
            {
                Initialize(0);
            }
            Debug.Assert(_buckets != null);

            Entry[] entries = _entries;
            Debug.Assert(entries != null, "expected entries to be non-null");

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
                    // NOTE: this must add EVEN IF it is already present,
                    // as it may be a different object with the same name,
                    // and we want "last wins" semantics
                    entries[i].Value = value;
                    return false;
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
                Debug.Assert((StartOfFreeList - entries![_freeList].Next) >= -1, "shouldn't overflow because `next` cannot underflow");
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
                ref Entry entry = ref entries![index];
                entry.HashCode = hashCode;
                entry.Next = bucket - 1; // Value in _buckets is 1-based
                entry.Value = value;
                bucket = index + 1;
                _version++;
            }

            return true;
        }

        /// <summary>
        /// Equality comparer against another of this type.
        /// Compares entries by reference - not merely by using the comparer on the key
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

            T ours;
            foreach (T element in other)
            {
                if (!TryGetValue(element.Key, out ours) || !ReferenceEquals(element, ours))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if equality comparers are equal. This is used for algorithms that can
        /// speed up if it knows the other item has unique elements. I.e. if they're using
        /// different equality comparers, then uniqueness assumption between sets break.
        /// </summary>
        internal static bool EqualityComparersAreEqual(RetrievableEntryHashSet<T> set1, RetrievableEntryHashSet<T> set2) => set1._comparer.Equals(set2._comparer);

        private int InternalGetHashCode(string item, int index, int length)
        {
            // No need to check for null 'item' as we own all comparers
            if (_constrainedComparer != null)
            {
                return _constrainedComparer.GetHashCode(item, index, length);
            }

            return (item == null) ? 0 : _comparer.GetHashCode(item);
        }

        #endregion

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
            private T _current;

            internal Enumerator(RetrievableEntryHashSet<T> hashSet)
            {
                _hashSet = hashSet;
                _version = hashSet._version;
                _index = 0;
                _current = default!;
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
                    ref Entry entry = ref _hashSet._entries![_index++];
                    if (entry.Next >= -1)
                    {
                        _current = entry.Value;
                        return true;
                    }
                }

                _index = _hashSet._count + 1;
                _current = default!;
                return false;
            }

            public T Current => _current;

            public void Dispose() { }

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _hashSet._count + 1))
                    {
                        throw new InvalidOperationException();
                    }

                    return _current;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _hashSet._version)
                {
                    throw new InvalidOperationException();
                }

                _index = 0;
                _current = default!;
            }
        }
    }
}
