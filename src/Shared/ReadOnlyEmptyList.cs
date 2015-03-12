// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A read-only empty list</summary>
//-----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A special singleton read-only empty list
    /// </summary>
    /// <typeparam name="T">Type of item</typeparam>
    [DebuggerDisplay("Count = 0")]
    internal class ReadOnlyEmptyList<T> : IList<T>, ICollection<T>, ICollection
    {
        /// <summary>
        /// The single instance
        /// </summary>
        private static ReadOnlyEmptyList<T> s_instance;

        /// <summary>
        /// Private default constructor as this is a singleton
        /// </summary>
        private ReadOnlyEmptyList()
        {
        }

        /// <summary>
        /// Get the instance
        /// </summary>
        public static ReadOnlyEmptyList<T> Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new ReadOnlyEmptyList<T>();
                }

                return s_instance;
            }
        }

        /// <summary>
        /// There are no items in this list
        /// </summary>
        public int Count
        {
            get { return 0; }
        }

        /// <summary>
        /// Read-only list
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// ICollection implementation
        /// </summary>
        int ICollection.Count
        {
            get { return 0; }
        }

        /// <summary>
        /// ICollection implementation
        /// </summary>
        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// ICollection implementation
        /// </summary>
        object ICollection.SyncRoot
        {
            get { return null; }
        }

        /// <summary>
        /// Items cannot be retrieved or added to a read-only list
        /// </summary>
        public T this[int index]
        {
            get
            {
                ErrorUtilities.ThrowArgumentOutOfRange("index");
                return default(T);
            }

            set
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            }
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
        /// Index of specified item
        /// </summary>
        public int IndexOf(T item)
        {
            return -1;
        }

        /// <summary>
        /// Items cannot be inserted into a read-only list
        /// </summary>
        public void Insert(int index, T item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Items cannot be removed from a read-only list
        /// </summary>
        public void RemoveAt(int index)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Items cannot be added to a read-only list
        /// </summary>
        public void Add(T item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Read-only list cannot be cleared
        /// </summary>
        public void Clear()
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// An empty list contains nothing
        /// </summary>
        public bool Contains(T item)
        {
            return false;
        }

        /// <summary>
        /// An empty list copies nothing
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
        }

        /// <summary>
        /// Cannot remove items from a read-only list
        /// </summary>
        public bool Remove(T item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        /// <summary>
        /// ICollection implementation
        /// </summary>
        void ICollection.CopyTo(System.Array array, int index)
        {
            // Do nothing
        }
    }
}