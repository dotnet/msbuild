// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

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
                // If value is not a list, it is either null or a single item.
                int count = (_value is IList<I> list) ? list.Count : (_value is null ? 0 : 1);
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
            if (_value is null)
            {
                _value = item;
            }
            else
            {
                if (_value is not List<I> list)
                {
                    list = new List<I>()
                    {
                        (I)_value
                    };
                    _value = list;
                }
                list.Add(item);
            }
        }

        public void Delete(I item)
        {
            if (_value is List<I> list)
            {
                list.Remove(item);
            }
            else if (object.Equals(_value, item))
            {
                _value = null;
            }
        }

        public void Replace(I oldItem, I newItem)
        {
            if (_value is List<I> list)
            {
                int index = list.IndexOf(oldItem);
                if (index >= 0)
                {
                    list[index] = newItem;
                }
            }
            else if (object.Equals(_value, oldItem))
            {
                _value = newItem;
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_value);
        }
    }
}
