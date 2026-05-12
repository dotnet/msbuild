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
    /// A class which implements IEnumerable by creating a copy of the backing collection.
    /// </summary>
    /// <remarks>
    /// <see cref="GetEnumerator()"/> is thread safe for concurrent access.
    /// </remarks>
    /// <typeparam name="TSource">The type contained in the backing collection.</typeparam>
    /// <typeparam name="TResult">The type of items being enumerated.</typeparam>
    internal class CopyOnReadEnumerable<TSource, TResult> : IEnumerable<TResult>
    {
        /// <summary>
        /// The backing collection.
        /// </summary>
        private readonly IEnumerable<TSource> _backingEnumerable;

        /// <summary>
        /// The object used to synchronize access for copying.
        /// </summary>
        private readonly object _syncRoot;

        /// <summary>
        /// The function to translate items in the backing collection to the resulting type.
        /// </summary>
        private readonly Func<TSource, TResult> _selector;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="backingEnumerable">The collection which serves as a source for enumeration.</param>
        /// <param name="syncRoot">The object used to synchronize access for copying.</param>
        /// <param name="selector">function to translate items in the backing collection to the resulting type.</param>
        public CopyOnReadEnumerable(IEnumerable<TSource> backingEnumerable, object syncRoot, Func<TSource, TResult> selector)
        {
            ErrorUtilities.VerifyThrowArgumentNull(backingEnumerable);
            ErrorUtilities.VerifyThrowArgumentNull(syncRoot);

            _backingEnumerable = backingEnumerable;
            _syncRoot = syncRoot;
            _selector = selector;
        }

        #region IEnumerable<T> Members

        /// <summary>
        /// Returns an enumerator over the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<TResult> GetEnumerator()
        {
            List<TResult> list;

#if NET
            if (_backingEnumerable.TryGetNonEnumeratedCount(out int count))
            {
#else
            if (_backingEnumerable is ICollection backingCollection)
            {
                int count = backingCollection.Count;
#endif
                list = new List<TResult>(count);
            }
            else if (_backingEnumerable is ICollection<TSource> collection)
            {
                list = new List<TResult>(collection.Count);
            }
            else if (_backingEnumerable is IReadOnlyCollection<TSource> readOnlyCollection)
            {
                list = new List<TResult>(readOnlyCollection.Count);
            }
            else
            {
                list = new List<TResult>();
            }

            lock (_syncRoot)
            {
                list.AddRange(_backingEnumerable.Select(_selector));
            }

            return list.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an numerator over the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TResult>)this).GetEnumerator();
        }

        #endregion
    }
}
