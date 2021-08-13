// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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
                // If value is not a list, it is a single item.
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

        public bool IsEmpty => _value == null || (_value is List<I> list && list.Count == 0);

        public ItemDataCollectionValue(I item)
        {
            _value = item;
        }

        public void Add(I item)
        {
            if (_value is not List<I> list)
            {
                list = new List<I>();
                _value = list;
            }
            list.Add(item);
        }

        public void Delete(I item)
        {
            if (object.ReferenceEquals(_value, item))
            {
                _value = null;
            }
            else if (_value is List<I> list)
            {
                list.Remove(item);
            }
        }

        public void Replace(I oldItem, I newItem)
        {
            if (object.ReferenceEquals(_value, oldItem))
            {
                _value = newItem;
            }
            else if (_value is List<I> list)
            {
                int index = list.FindIndex(item => object.ReferenceEquals(item, oldItem));
                if (index >= 0)
                {
                    list[index] = newItem;
                }
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_value);
        }
    }
}
