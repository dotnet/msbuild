// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A read-only live wrapper over a collection.
    /// It does not prevent modification of the values themselves.
    /// </summary>
    /// <remarks>
    /// There is a type with the same name in the BCL, but it is actually a ReadOnlyList and does not accept an ICollection&gt;T&lt;.
    /// Thus this is an omission from the BCL.
    /// </remarks>
    /// <typeparam name="T">Type of element in the collection</typeparam>
    internal sealed class ReadOnlyCollection<T> : ICollection<T>, ICollection
    {
        /// <summary>
        /// Backing live enumerable.
        /// May be a collection.
        /// </summary>
        private IEnumerable<T> _backing;

        /// <summary>
        /// Construct a read only wrapper around the current contents
        /// of the IEnumerable, or around the backing collection if the
        /// IEnumerable is in fact a collection.
        /// </summary>
        internal ReadOnlyCollection(IEnumerable<T> backing)
        {
            ErrorUtilities.VerifyThrow(backing != null, "Need backing collection");

            _backing = backing;
        }

        /// <summary>
        /// Return the number of items in the backing collection
        /// </summary>
        public int Count
        {
            get
            {
                return BackingCollection.Count;
            }
        }

        /// <summary>
        /// Returns true.
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Whether collection is synchronized
        /// </summary>
        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// Sync root
        /// </summary>
        object ICollection.SyncRoot
        {
            get { return this; }
        }

        /// <summary>
        /// Get a backing ICollection.
        /// </summary>
        private ICollection<T> BackingCollection
        {
            get
            {
                ICollection<T> backingCollection = _backing as ICollection<T>;
                if (backingCollection == null)
                {
                    backingCollection = new List<T>(_backing);
                    _backing = backingCollection;
                }

                return backingCollection;
            }
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public void Add(T item)
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
        /// Pass through for underlying collection
        /// </summary>
        public bool Contains(T item)
        {
            // UNDONE: IEnumerable.Contains<T>() does the ICollection check,
            // so we could just use IEnumerable.Contains<T>() here.
            if (!(_backing is ICollection<T>))
            {
                return _backing.Contains<T>(item);
            }

            return BackingCollection.Contains(item);
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
            ErrorUtilities.VerifyThrowArgumentNull(array, nameof(array));

            ICollection<T> backingCollection = _backing as ICollection<T>;
            if (backingCollection != null)
            {
                backingCollection.CopyTo(array, arrayIndex);
            }
            else
            {
                int i = arrayIndex;
                foreach (T entry in _backing)
                {
                    array[i] = entry;
                    i++;
                }
            }
        }

        /// <summary>
        /// Prohibited on read only collection: throws
        /// </summary>
        public bool Remove(T item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        /// <comment>
        /// NOTE: This does NOT cause a copy into a List, since the
        /// backing enumerable suffices.
        /// </comment>
        public IEnumerator<T> GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        /// <comment>
        /// NOTE: This does NOT cause a copy into a List, since the
        /// backing enumerable suffices.
        /// </comment>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_backing).GetEnumerator();
        }

        /// <summary>
        /// ICollection version of CopyTo
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        {
            ErrorUtilities.VerifyThrowArgumentNull(array, nameof(array));

            int i = index;
            foreach (T entry in _backing)
            {
                array.SetValue(entry, i);
                i++;
            }
        }
    }
}
