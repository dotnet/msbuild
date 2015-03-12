// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Enumerable providing a filtered view of a collection</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// An enumerable over the provided collection that only exposes the members of the
    /// collection that have the type for which it is specialized for
    /// </summary>
    /// <remarks>
    /// If the 'is' checks are expensive, a field containing a type enumeration could be used instead.
    /// </remarks>
    /// <typeparam name="Base">Type of element in the underlying collection</typeparam>
    /// <typeparam name="Filter">Type to filter for</typeparam>
    internal struct FilteringEnumerable<Base, Filter> : IEnumerable<Filter>
        where Filter : class, Base
    {
        /// <summary>
        /// Backing collection
        /// </summary>
        private readonly IEnumerable<Base> _enumerable;

        /// <summary>
        /// Constructor accepting the backing collection
        /// Backing collection may be null, indicating an empty collection
        /// </summary>
        internal FilteringEnumerable(IEnumerable<Base> enumerable)
        {
            _enumerable = enumerable;
        }

        /// <summary>
        /// Gets an enumerator over all the elements in the backing collection that meet
        /// the filter criteria
        /// </summary>
        public IEnumerator<Filter> GetEnumerator()
        {
            if (_enumerable == null)
            {
                return ReadOnlyEmptyList<Filter>.Instance.GetEnumerator();
            }

            return new FilteringEnumerator<Base, Filter>(_enumerable.GetEnumerator());
        }

        /// <summary>
        /// Gets an enumerator over all the elements in the backing collection that meet
        /// the filter criteria
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Custom enumerator that allows enumeration over only the items in
        /// the collection that are of the type it is specialized for.
        /// </summary>
        /// <typeparam name="Base2">Type of element in the underlying collection</typeparam>
        /// <typeparam name="Filter2">Type to filter for</typeparam>
        private struct FilteringEnumerator<Base2, Filter2> : IEnumerator<Filter2>
            where Filter2 : class, Base2
        {
            /// <summary>
            /// The real enumerator
            /// </summary>
            private IEnumerator<Base2> _enumerator;

            /// <summary>
            /// Constructor initializing with the real enumerator
            /// </summary>
            internal FilteringEnumerator(IEnumerator<Base2> enumerator)
            {
                _enumerator = enumerator;
            }

            /// <summary>
            /// Get the current, if any, otherwise null
            /// </summary>
            /// <remarks>
            /// Current is undefined if enumerator is before the start of the collection
            /// or if MoveNext() returned false
            /// </remarks>
            public Filter2 Current
            {
                get { return _enumerator.Current as Filter2; }
            }

            /// <summary>
            /// Get the current, if any
            /// </summary>
            /// <remarks>
            /// Current is undefined if enumerator is before the start of the collection
            /// or if MoveNext() returned false
            /// </remarks>
            object System.Collections.IEnumerator.Current
            {
                get { return _enumerator.Current; }
            }

            /// <summary>
            /// Dispose
            /// </summary>
            public void Dispose()
            {
                _enumerator.Dispose();
            }

            /// <summary>
            /// Move to the next object of the specialized type, if any, and return true;
            /// otherwise return false
            /// </summary>
            public bool MoveNext()
            {
                bool result;
                do
                {
                    result = _enumerator.MoveNext();
                }
                while (result && !(_enumerator.Current is Filter2));

                return result;
            }

            /// <summary>
            /// Reset
            /// </summary>
            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }
}
