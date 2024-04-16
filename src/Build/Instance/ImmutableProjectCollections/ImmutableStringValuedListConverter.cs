// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    internal class ImmutableStringValuedListConverter<T> : IList<string>, IReadOnlyList<string>
    {
        private readonly IList<T> _itemList;
        private readonly Func<T, string> _getStringValue;

        public ImmutableStringValuedListConverter(IList<T> itemList, Func<T, string> getStringValue)
        {
            _itemList = itemList;
            _getStringValue = getStringValue;
        }

        public string this[int index]
        {
            set => throw new NotSupportedException();
            get => _getStringValue(_itemList[index]);
        }

        public int Count => _itemList.Count;

        public bool IsReadOnly => true;

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public void Insert(int index, string item) => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public bool Contains(string item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _itemList.Count);

            int currentIndex = arrayIndex;
            foreach (var item in _itemList)
            {
                string? stringValue = _getStringValue(item);
                if (stringValue != null)
                {
                    array[currentIndex] = stringValue;
                    ++currentIndex;
                }
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var item in _itemList)
            {
                string? stringValue = _getStringValue(item);
                if (stringValue != null)
                {
                    yield return stringValue;
                }
            }
        }

        public int IndexOf(string item)
        {
            for (int i = 0; i < _itemList.Count; ++i)
            {
                T cachedItem = _itemList[i];
                string stringValue = _getStringValue(cachedItem);
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(stringValue, item))
                {
                    return i;
                }
            }

            return -1;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var item in _itemList)
            {
                string? instance = _getStringValue(item);
                if (instance != null)
                {
                    yield return instance;
                }
            }
        }
    }
}
