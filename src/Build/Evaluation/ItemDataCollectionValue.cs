// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An efficient multi-value wrapper holding one or more items.
    /// </summary>
    internal struct ItemDataCollectionValue<I>
    {
        /// <summary>
        /// A non-allocating enumerator for the multi-value.
        /// </summary>
        public struct Enumerator : IEnumerator<I>
        {
            private object _value;
            private int _index;

            public Enumerator(object value)
            {
                _value = value;
                _index = -1;
            }

            public I Current => (_value is IList<I> list) ? list[_index] : (I)_value;
            object System.Collections.IEnumerator.Current => Current;

            public void Dispose()
            { }

            public bool MoveNext()
            {
                int count = (_value is IList<I> list) ? list.Count : 1;
                if (_index + 1 < count)
                {
                    _index++;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                _index = -1;
            }
        }

        /// <summary>
        /// Holds one value or a list of values.
        /// </summary>
        private object _value;

        public bool IsEmpty => _value == null || (_value is ImmutableList<I> list && list.Count == 0);

        public ItemDataCollectionValue(I item)
        {
            _value = item;
        }

        public void Add(I item)
        {
            if (_value is not ImmutableList<I> list)
            {
                list = ImmutableList<I>.Empty;
                list = list.Add((I)_value);
            }
            _value = list.Add(item);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_value);
        }
    }
}
