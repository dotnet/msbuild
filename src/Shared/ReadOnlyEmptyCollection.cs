// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A read-only wrapper over an empty collection.
    /// </summary>
    /// <remarks>
    /// Thus this is an omission from the BCL.
    /// </remarks>
    /// <typeparam name="T">Type of element in the collection</typeparam>
    internal class ReadOnlyEmptyCollection<T> : ICollection<T>, ICollection
    {
        /// <summary>
        /// Backing live collection
        /// </summary>
        private static ReadOnlyEmptyCollection<T> s_instance;

        /// <summary>
        /// Private default constructor as this is a singleton
        /// </summary>
        private ReadOnlyEmptyCollection()
        {
        }

        /// <summary>
        /// Get the instance
        /// </summary>
        public static ReadOnlyEmptyCollection<T> Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new ReadOnlyEmptyCollection<T>();
                }

                return s_instance;
            }
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        public int Count
        {
            get { return 0; }
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
            return false;
        }

        /// <summary>
        /// Pass through for underlying collection
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
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
        /// Get an enumerator over an empty collection
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            yield break;
        }

        /// <summary>
        /// Get an enumerator over an empty collection
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// ICollection version of CopyTo
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        {
        }
    }
}
