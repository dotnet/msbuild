// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A class which implements IEnumerable by creating an optionally-deep copy of the backing collection.</summary>
//-----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A class which implements IEnumerable by creating an optionally-deep copy of the backing collection.
    /// </summary>
    /// <remarks>
    /// If the type contained in the collection implements IDeepCloneable then the copies will be deep clones instead
    /// of mere reference copies.
    /// <see cref="GetEnumerator()"/> is thread safe for concurrent access.
    /// </remarks>
    /// <typeparam name="T">The type contained in the backing collection.</typeparam>
    internal class CopyOnReadEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// The backing collection.
        /// </summary>
        private IEnumerable<T> _backingEnumerable;

        /// <summary>
        /// The object used to synchronize access for copying.
        /// </summary>
        private object _syncRoot;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="backingEnumerable">The collection which serves as a source for enumeration.</param>
        /// <param name="syncRoot">The object used to synchronize access for copying.</param>
        public CopyOnReadEnumerable(IEnumerable<T> backingEnumerable, object syncRoot)
        {
            ErrorUtilities.VerifyThrowArgumentNull(backingEnumerable, "backingCollection");
            ErrorUtilities.VerifyThrowArgumentNull(syncRoot, "syncRoot");

            _backingEnumerable = backingEnumerable;
            _syncRoot = syncRoot;
        }

        #region IEnumerable<T> Members

        /// <summary>
        /// Returns an enumerator over the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            List<T> list;
            ICollection backingCollection = _backingEnumerable as ICollection;
            if (backingCollection != null)
            {
                list = new List<T>(backingCollection.Count);
            }
            else
            {
                list = new List<T>();
            }

            bool isCloneable = false;
            bool checkForCloneable = true;
            lock (_syncRoot)
            {
                foreach (T item in _backingEnumerable)
                {
                    if (checkForCloneable)
                    {
                        if (item is IDeepCloneable<T>)
                        {
                            isCloneable = true;
                        }

                        checkForCloneable = false;
                    }

                    T copiedItem = isCloneable ? (item as IDeepCloneable<T>).DeepClone() : item;
                    list.Add(copiedItem);
                }
            }

            return list.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an numerator over the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        #endregion
    }
}
