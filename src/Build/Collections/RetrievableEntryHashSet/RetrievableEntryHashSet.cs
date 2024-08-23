﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using System.Security;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

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

#nullable disable

// The BuildXL package causes an indirect dependency on the RuntimeContracts package, which adds an analyzer which forbids the use of System.Diagnostics.Contract.
// So effectively if your dependencies use RuntimeContracts, it attempts to force itself on your as well.
// See: https://github.com/SergeyTeplyakov/RuntimeContracts/issues/12
#pragma warning disable RA001 // Do not use System.Diagnostics.Contract class.

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
    [DebuggerTypeProxy(typeof(Microsoft.Build.Collections.HashSetDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "By design")]
    [Serializable()]
#if FEATURE_SECURITY_PERMISSIONS
    [System.Security.Permissions.HostProtection(MayLeakOnAbort = true)]
#endif
    internal class RetrievableEntryHashSet<T> : IRetrievableEntryHashSet<T>
        where T : class, IKeyed
    {
        // store lower 31 bits of hash code
        private const int Lower31BitMask = 0x7FFFFFFF;

        // when constructing a hashset from an existing collection, it may contain duplicates,
        // so this is used as the max acceptable excess ratio of capacity to count. Note that
        // this is only used on the ctor and not to automatically shrink if the hashset has, e.g,
        // a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess.
        // This is set to 3 because capacity is acceptable as 2x rounded up to nearest prime.
        private const int ShrinkThreshold = 3;

        // constants for serialization
        private const String CapacityName = "Capacity";
        private const String ElementsName = "Elements";
        private const String ComparerName = "Comparer";
        private const String VersionName = "Version";

        private int[] _buckets;
        private Slot[] _slots;
        private int _count;
        private int _lastIndex;
        private int _freeList;
        private IEqualityComparer<string> _comparer;
        private IConstrainedEqualityComparer<string> _constrainedComparer;
        private int _version;
        private bool _readOnly;

        // temporary variable needed during deserialization
        private SerializationInfo _siInfo;

        #region Constructors

        public RetrievableEntryHashSet(IEqualityComparer<string> comparer)
        {
            if (comparer == null)
            {
                ErrorUtilities.ThrowInternalError("use explicit comparer");
            }

            _comparer = comparer;
            _constrainedComparer = comparer as IConstrainedEqualityComparer<string>;
            _lastIndex = 0;
            _count = 0;
            _freeList = -1;
            _version = 0;
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
        /// the need to resize by setting the initial capacity based on size of collection.
        /// </summary>
        public RetrievableEntryHashSet(IEnumerable<T> collection, IEqualityComparer<string> comparer)
            : this(comparer)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            Contract.EndContractBlock();

            // to avoid excess resizes, first set size based on collection's count. Collection
            // may contain duplicates, so call TrimExcess if resulting hashset is larger than
            // threshold
            int suggestedCapacity = 0;
            ICollection<T> coll = collection as ICollection<T>;
            if (coll != null)
            {
                suggestedCapacity = coll.Count;
            }
            Initialize(suggestedCapacity);

            this.UnionWith(collection);
            if ((_count == 0 && _slots.Length > HashHelpers.GetMinPrime()) ||
                (_count > 0 && _slots.Length / _count > ShrinkThreshold))
            {
                TrimExcess();
            }
        }

        protected RetrievableEntryHashSet(SerializationInfo info, StreamingContext context)
        {
            // We can't do anything with the keys and values until the entire graph has been
            // deserialized and we have a reasonable estimate that GetHashCode is not going to
            // fail.  For the time being, we'll just cache this.  The graph is not valid until
            // OnDeserialization has been called.
            _siInfo = info;
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
        public ICollection<T> Values
        {
            get { return this; }
        }

        #region ICollection<T> methods

        // Convenience to minimise change to callers used to dictionaries
        internal T this[string name]
        {
            get
            {
                return Get(name);
            }

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
        void ICollection<T>.Add(T item)
        {
            AddEvenIfPresent(item);
        }

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

            if (_lastIndex > 0)
            {
                Debug.Assert(_buckets != null, "m_buckets was null but m_lastIndex > 0");

                // clear the elements so that the gc can reclaim the references.
                // clear only up to m_lastIndex for m_slots
                Array.Clear(_slots, 0, _lastIndex);
                Array.Clear(_buckets, 0, _buckets.Length);
                _lastIndex = 0;
                _count = 0;
                _freeList = -1;
            }
            _version++;
        }

        // Convenience
        internal bool Contains(string key)
        {
            return Get(key) != null;
        }

        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> entry)
        {
            Debug.Assert(String.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Get(entry.Value.Key) != null;
        }

        public bool ContainsKey(string key)
        {
            return Get(key) != null;
        }

        T IDictionary<string, T>.this[string name]
        {
            get { return Get(name); }
            set { Add(value); }
        }

        /// <summary>
        /// Checks if this hashset contains the item
        /// </summary>
        /// <param name="item">item to check for containment</param>
        /// <returns>true if item contained; false if not</returns>
        public bool Contains(T item)
        {
            return Get(item.Key) != null;
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
        /// <returns>The item, if it was found. Otherwise, default(T).</returns>
        public T Get(string key)
        {
            return GetCore(key, 0, key?.Length ?? 0);
        }

        /// <summary>
        /// Gets the item if any with the given name
        /// </summary>
        /// <param name="key">key to check for containment</param>
        /// <param name="index">The position of the substring within <paramref name="key"/>.</param>
        /// <param name="length">The maximum number of characters in the <paramref name="key"/> to lookup.</param>
        /// <returns>true if item contained; false if not</returns>
        public T Get(string key, int index, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (index < 0 || index > (key == null ? 0 : key.Length) - length)
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
        /// <returns>true if item contained; false if not</returns>
        private T GetCore(string item, int index, int length)
        {
            if (_buckets != null)
            {
                int hashCode = InternalGetHashCode(item, index, length);
                // see note at "HashSet" level describing why "- 1" appears in for loop
                for (int i = _buckets[hashCode % _buckets.Length] - 1; i >= 0; i = _slots[i].next)
                {
                    if (_slots[i].hashCode == hashCode && _constrainedComparer != null ? _constrainedComparer.Equals(_slots[i].value.Key, item, index, length) : _comparer.Equals(_slots[i].value.Key, item))
                    {
                        return _slots[i].value;
                    }
                }
            }
            // either m_buckets is null or wasn't found
            return default(T);
        }

        /// <summary>
        /// Copy items in this hashset to array, starting at arrayIndex
        /// </summary>
        /// <param name="array">array to add items to</param>
        /// <param name="arrayIndex">index to start at</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            CopyTo(array, arrayIndex, _count);
        }

        /// <summary>
        /// Remove entry that compares equal to T
        /// </summary>
        public bool Remove(T item)
        {
            return Remove(item.Key);
        }

        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> entry)
        {
            Debug.Assert(String.Equals(entry.Key, entry.Value.Key, StringComparison.Ordinal));
            return Remove(entry.Value);
        }

        /// <summary>
        /// Remove item from this hashset
        /// </summary>
        /// <param name="item">item to remove</param>
        /// <returns>true if removed; false if not (i.e. if the item wasn't in the HashSet)</returns>
        public bool Remove(string item)
        {
            if (_readOnly)
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            }

            if (_buckets != null)
            {
                int hashCode = InternalGetHashCode(item);
                int bucket = hashCode % _buckets.Length;
                int last = -1;
                for (int i = _buckets[bucket] - 1; i >= 0; last = i, i = _slots[i].next)
                {
                    if (_slots[i].hashCode == hashCode && _comparer.Equals(_slots[i].value.Key, item))
                    {
                        if (last < 0)
                        {
                            // first iteration; update buckets
                            _buckets[bucket] = _slots[i].next + 1;
                        }
                        else
                        {
                            // subsequent iterations; update 'next' pointers
                            _slots[last].next = _slots[i].next;
                        }
                        _slots[i].hashCode = -1;
                        _slots[i].value = default(T);
                        _slots[i].next = _freeList;

                        _count--;
                        _version++;
                        if (_count == 0)
                        {
                            _lastIndex = 0;
                            _freeList = -1;
                        }
                        else
                        {
                            _freeList = i;
                        }
                        return true;
                    }
                }
            }
            // either m_buckets is null or wasn't found
            return false;
        }

        /// <summary>
        /// Number of elements in this hashset
        /// </summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>
        /// Whether this is readonly
        /// </summary>
        public bool IsReadOnly
        {
            get { return _readOnly; }
        }

        /// <summary>
        /// Permanently prevent changes to the set.
        /// </summary>
        internal void MakeReadOnly()
        {
            _readOnly = true;
        }

        #endregion

        #region IEnumerable methods

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
        {
            foreach (var entry in this)
            {
                yield return new KeyValuePair<string, T>(entry.Key, entry);
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region ISerializable methods

        // [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        [SecurityCritical]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            // need to serialize version to avoid problems with serializing while enumerating
            info.AddValue(VersionName, _version);
            info.AddValue(ComparerName, _comparer, typeof(IEqualityComparer<string>));
            info.AddValue(CapacityName, _buckets == null ? 0 : _buckets.Length);
            if (_buckets != null)
            {
                T[] array = new T[_count];
                CopyTo(array);
                info.AddValue(ElementsName, array, typeof(T[]));
            }
        }

        #endregion

        #region IDeserializationCallback methods

        public virtual void OnDeserialization(Object sender)
        {
            if (_siInfo == null)
            {
                // It might be necessary to call OnDeserialization from a container if the
                // container object also implements OnDeserialization. However, remoting will
                // call OnDeserialization again. We can return immediately if this function is
                // called twice. Note we set m_siInfo to null at the end of this method.
                return;
            }

            int capacity = _siInfo.GetInt32(CapacityName);
            _comparer = (IEqualityComparer<string>)_siInfo.GetValue(ComparerName, typeof(IEqualityComparer<string>));
            _constrainedComparer = _comparer as IConstrainedEqualityComparer<string>;
            _freeList = -1;

            if (capacity != 0)
            {
                _buckets = new int[capacity];
                _slots = new Slot[capacity];

                T[] array = (T[])_siInfo.GetValue(ElementsName, typeof(T[]));

                if (array == null)
                {
                    throw new SerializationException();
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

            _version = _siInfo.GetInt32(VersionName);
            _siInfo = null;
        }

        #endregion

        #region HashSet methods

        /// <summary>
        /// Add item to this HashSet.
        /// *** MSBUILD NOTE: Always added - overwrite semantics
        /// </summary>
        public void Add(T item)
        {
            AddEvenIfPresent(item);
        }

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
        ///
        /// Implementation note: GetSuggestedCapacity (to increase capacity in advance avoiding
        /// multiple resizes ended up not being useful in practice; quickly gets to the
        /// point where it's a wasteful check.
        /// </summary>
        /// <param name="other">enumerable with items to add</param>
        public void UnionWith(IEnumerable<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            Contract.EndContractBlock();

            foreach (T item in other)
            {
                AddEvenIfPresent(item);
            }
        }

        // Copy all elements into array starting at zero based index specified
        [SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "Decently informative for an exception that will probably never actually see the light of day")]
        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int index)
        {
            if (index < 0 || Count > array.Length - index)
            {
                throw new ArgumentException("index");
            }

            int i = index;
            foreach (var entry in this)
            {
                array[i] = new KeyValuePair<string, T>(entry.Key, entry);
                i++;
            }
        }

        public void CopyTo(T[] array) { CopyTo(array, 0, _count); }

        [SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "Decently informative for an exception that will probably never actually see the light of day")]
        public void CopyTo(T[] array, int arrayIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            Contract.EndContractBlock();

            // check array index valid index into array
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            // also throw if count less than 0
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // will array, starting at arrayIndex, be able to hold elements? Note: not
            // checking arrayIndex >= array.Length (consistency with list of allowing
            // count of 0; subsequent check takes care of the rest)
            if (arrayIndex > array.Length || count > array.Length - arrayIndex)
            {
                throw new ArgumentException("arrayIndex");
            }

            int numCopied = 0;
            for (int i = 0; i < _lastIndex && numCopied < count; i++)
            {
                if (_slots[i].hashCode >= 0)
                {
                    array[arrayIndex + numCopied] = _slots[i].value;
                    numCopied++;
                }
            }
        }

        /// <summary>
        /// Sets the capacity of this list to the size of the list (rounded up to nearest prime),
        /// unless count is 0, in which case we release references.
        ///
        /// This method can be used to minimize a list's memory overhead once it is known that no
        /// new elements will be added to the list. To completely clear a list and release all
        /// memory referenced by the list, execute the following statements:
        ///
        /// list.Clear();
        /// list.TrimExcess();
        /// </summary>
        public void TrimExcess()
        {
            Debug.Assert(_count >= 0, "m_count is negative");

            if (_count == 0)
            {
                // if count is zero, clear references
                _buckets = null;
                _slots = null;
                _version++;
            }
            else
            {
                Debug.Assert(_buckets != null, "m_buckets was null but m_count > 0");

                // similar to IncreaseCapacity but moves down elements in case add/remove/etc
                // caused fragmentation
                int newSize = HashHelpers.GetPrime(_count);
                Slot[] newSlots = new Slot[newSize];
                int[] newBuckets = new int[newSize];

                // move down slots and rehash at the same time. newIndex keeps track of current
                // position in newSlots array
                int newIndex = 0;
                for (int i = 0; i < _lastIndex; i++)
                {
                    if (_slots[i].hashCode >= 0)
                    {
                        newSlots[newIndex] = _slots[i];

                        // rehash
                        int bucket = newSlots[newIndex].hashCode % newSize;
                        newSlots[newIndex].next = newBuckets[bucket] - 1;
                        newBuckets[bucket] = newIndex + 1;

                        newIndex++;
                    }
                }

                Debug.Assert(newSlots.Length <= _slots.Length, "capacity increased after TrimExcess");

                _lastIndex = newIndex;
                _slots = newSlots;
                _buckets = newBuckets;
                _freeList = -1;
            }
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
        /// greater than or equal to capacity.
        /// </summary>
        /// <param name="capacity"></param>
        private void Initialize(int capacity)
        {
            Debug.Assert(_buckets == null, "Initialize was called but m_buckets was non-null");

            int size = HashHelpers.GetPrime(capacity);

            _buckets = new int[size];
            _slots = new Slot[size];
        }

        /// <summary>
        /// Expand to new capacity. New capacity is next prime greater than or equal to suggested
        /// size. This is called when the underlying array is filled. This performs no
        /// defragmentation, allowing faster execution; note that this is reasonable since
        /// AddEvenIfPresent attempts to insert new elements in re-opened spots.
        /// </summary>
        private void IncreaseCapacity()
        {
            Debug.Assert(_buckets != null, "IncreaseCapacity called on a set with no elements");

            int newSize = HashHelpers.ExpandPrime(_count);
            if (newSize <= _count)
            {
                throw new ArgumentException("newSize");
            }

            // Able to increase capacity; copy elements to larger array and rehash
            Slot[] newSlots = new Slot[newSize];
            if (_slots != null)
            {
                Array.Copy(_slots, 0, newSlots, 0, _lastIndex);
            }

            int[] newBuckets = new int[newSize];
            for (int i = 0; i < _lastIndex; i++)
            {
                int bucket = newSlots[i].hashCode % newSize;
                newSlots[i].next = newBuckets[bucket] - 1;
                newBuckets[bucket] = i + 1;
            }
            _slots = newSlots;
            _buckets = newBuckets;
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

            string key = value.Key;
            int hashCode = InternalGetHashCode(key);
            int bucket = hashCode % _buckets.Length;
            for (int i = _buckets[hashCode % _buckets.Length] - 1; i >= 0; i = _slots[i].next)
            {
                if (_slots[i].hashCode == hashCode && _comparer.Equals(_slots[i].value.Key, key))
                {
                    // NOTE: this must add EVEN IF it is already present,
                    // as it may be a different object with the same name,
                    // and we want "last wins" semantics
                    _slots[i].value = value;
                    return false;
                }
            }
            int index;
            if (_freeList >= 0)
            {
                index = _freeList;
                _freeList = _slots[index].next;
            }
            else
            {
                if (_lastIndex == _slots.Length)
                {
                    IncreaseCapacity();
                    // this will change during resize
                    bucket = hashCode % _buckets.Length;
                }
                index = _lastIndex;
                _lastIndex++;
            }
            _slots[index].hashCode = hashCode;
            _slots[index].value = value;
            _slots[index].next = _buckets[bucket] - 1;
            _buckets[bucket] = index + 1;
            _count++;
            _version++;
            return true;
        }

        /// <summary>
        /// Equality comparer against another of this type.
        /// Compares entries by reference - not merely by using the comparer on the key
        /// </summary>
        internal bool EntriesAreReferenceEquals(RetrievableEntryHashSet<T> other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.Count != other.Count)
            {
                return false;
            }

            T ours;
            foreach (T element in other)
            {
                if (!TryGetValue(element.Key, out ours) || !Object.ReferenceEquals(element, ours))
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Copies this to an array. Used for DebugView
        /// </summary>
        /// <returns></returns>
        internal T[] ToArray()
        {
            T[] newArray = new T[Count];
            CopyTo(newArray);
            return newArray;
        }

        private int InternalGetHashCode(string item, int index, int length)
        {
            // No need to check for null 'item' as we own all comparers
            if (_constrainedComparer != null)
            {
                return _constrainedComparer.GetHashCode(item, index, length) & Lower31BitMask;
            }

            return InternalGetHashCode(item);
        }

        /// <summary>
        /// Workaround Comparers that throw ArgumentNullException for GetHashCode(null).
        /// </summary>
        /// <param name="item"></param>
        /// <returns>hash code</returns>
        private int InternalGetHashCode(string item)
        {
            if (item == null)
            {
                return 0;
            }
            return _comparer.GetHashCode(item) & Lower31BitMask;
        }

        #endregion

        // used for set checking operations (using enumerables) that rely on counting
        internal struct ElementCount
        {
            internal int uniqueCount;
            internal int unfoundCount;
        }

        internal struct Slot
        {
            internal int hashCode;      // Lower 31 bits of hash code, -1 if unused
            internal T value;
            internal int next;          // Index of next entry, -1 if last
        }

#if !SILVERLIGHT
        [Serializable()]
#if FEATURE_SECURITY_PERMISSIONS
        [System.Security.Permissions.HostProtection(MayLeakOnAbort = true)]
#endif
#endif
        public struct Enumerator : IEnumerator<T>, System.Collections.IEnumerator
        {
            private RetrievableEntryHashSet<T> _set;
            private int _index;
            private int _version;
            private T _current;

            internal Enumerator(RetrievableEntryHashSet<T> set)
            {
                _set = set;
                _index = 0;
                _version = set._version;
                _current = default(T);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_version != _set._version)
                {
                    throw new InvalidOperationException();
                }

                while (_index < _set._lastIndex)
                {
                    if (_set._slots[_index].hashCode >= 0)
                    {
                        _current = _set._slots[_index].value;
                        _index++;
                        return true;
                    }
                    _index++;
                }
                _index = _set._lastIndex + 1;
                _current = default(T);
                return false;
            }

            public T Current
            {
                get
                {
                    return _current;
                }
            }

            Object System.Collections.IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _set._lastIndex + 1)
                    {
                        throw new InvalidOperationException();
                    }
                    return Current;
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                if (_version != _set._version)
                {
                    throw new InvalidOperationException();
                }

                _index = 0;
                _current = default(T);
            }
        }
    }
}
