// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A read-only collection wrapper which converts values as they are accessed.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A function that can be passed in so that the converting
    /// collection can do a "contains" operation on the backing
    /// collection, using an object of the "converted to" type.
    /// </summary>
    /// <typeparam name="N">Type converted to</typeparam>
    /// <returns>Whether the item is present</returns>
    internal delegate bool Contains<N>(N item);

    /// <summary>
    /// Implementation of ICollection which converts values to the specified type when they are accessed.
    /// </summary>
    /// <typeparam name="V">The backing collection's value type.</typeparam>
    /// <typeparam name="N">The desired value type.</typeparam>
    internal class ReadOnlyConvertingCollection<V, N> : ICollection<N>
    {
        /// <summary>
        /// The backing collection.
        /// </summary>
        private readonly ICollection<V> _backing;

        /// <summary>
        /// The delegate used to convert values.
        /// </summary>
        private readonly Func<V, N> _converter;

        /// <summary>
        /// The delegate used to satisfy contains operations, optionally
        /// </summary>
        private readonly Contains<N> _contains;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal ReadOnlyConvertingCollection(ICollection<V> backing, Func<V, N> converter)
            : this(backing, converter, null)
        {
        }

        /// <summary>
        /// Constructor, optionally taking a delegate to do a "backwards" contains operation.
        /// </summary>
        internal ReadOnlyConvertingCollection(ICollection<V> backing, Func<V, N> converter, Contains<N> contains)
        {
            ErrorUtilities.VerifyThrowArgumentNull(backing, "backing");
            ErrorUtilities.VerifyThrowArgumentNull(converter, "converter");

            _backing = backing;
            _converter = converter;
            _contains = contains;
        }

        #region ICollection<N> Members

        /// <summary>
        /// Return the number of items in the collection.
        /// </summary>
        public int Count
        {
            get { return _backing.Count; }
        }

        /// <summary>
        /// Returns true if the collection is readonly.
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Adds the specified item to the collection.
        /// </summary>
        public void Add(N item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Clears all items from the collection.
        /// </summary>
        public void Clear()
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
        }

        /// <summary>
        /// Returns true if the collection contains the speciied item.
        /// </summary>
        public bool Contains(N item)
        {
            if (_contains == null)
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedConvertingCollectionValueToBacking");
                return false;
            }

            return _contains(item);
        }

        /// <summary>
        /// Copy the elements of the collection to the specified array.
        /// </summary>
        public void CopyTo(N[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove the specified item from the collection.
        /// </summary>
        public bool Remove(N item)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            return false;
        }

        #endregion

        #region IEnumerable<N> Members

        /// <summary>
        /// Implementation of generic IEnumerable.GetEnumerator()
        /// </summary>
        public IEnumerator<N> GetEnumerator()
        {
            return new ConvertingEnumerable<V, N>(_backing, _converter).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Implementation of IEnumerable.GetEnumerator()
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<N>)this).GetEnumerator();
        }

        #endregion
    }
}
