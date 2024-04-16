// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    internal class ImmutableItemDefinitionsListConverter<TCached, T> : IList<T>
        where T : IKeyed
        where TCached : IKeyed
    {
        private readonly IList<TCached>? _itemList;
        private readonly TCached? _itemTypeDefinition;
        private readonly Func<TCached, T> _getInstance;

        public ImmutableItemDefinitionsListConverter(
            IList<TCached>? itemList,
            TCached? itemTypeDefinition,
            Func<TCached, T> getInstance)
        {
            ErrorUtilities.VerifyThrowArgumentNull(getInstance, nameof(getInstance));

            _itemList = itemList;
            _itemTypeDefinition = itemTypeDefinition;
            _getInstance = getInstance;
        }

        public T this[int index]
        {
            set => throw new NotSupportedException();
            get
            {
                if (_itemList == null)
                {
                    if (index != 0 || _itemTypeDefinition == null)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    return _getInstance(_itemTypeDefinition);
                }

                if (index > _itemList.Count)
                {
                    throw new IndexOutOfRangeException();
                }

                if (index == _itemList.Count)
                {
                    if (_itemTypeDefinition == null)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    return _getInstance(_itemTypeDefinition);
                }

                return _getInstance(_itemList[index]);
            }
        }

        public int Count => (_itemList == null ? 0 : _itemList.Count) + (_itemTypeDefinition == null ? 0 : 1);

        public bool IsReadOnly => true;

        public void Add(T item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public void Insert(int index, T item) => throw new NotSupportedException();

        public bool Remove(T item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), Count);

            int currentIndex = arrayIndex;
            void PutItemIntoArray(TCached item)
            {
                T? instance = _getInstance(item);
                if (instance != null)
                {
                    array[currentIndex] = instance;
                    ++currentIndex;
                }
            }

            if (_itemList != null)
            {
                foreach (var item in _itemList)
                {
                    PutItemIntoArray(item);
                }
            }

            if (_itemTypeDefinition != null)
            {
                PutItemIntoArray(_itemTypeDefinition);
            }
        }

        public IEnumerator<T> GetEnumerator() => GetEnumeratorImpl();

        public int IndexOf(T item)
        {
            int currentIndex = 0;
            if (_itemList != null)
            {
                foreach (var cachedItem in _itemList)
                {
                    if (IsMatchingItem(cachedItem, item))
                    {
                        return currentIndex;
                    }

                    ++currentIndex;
                }
            }

            if (_itemTypeDefinition != null)
            {
                if (IsMatchingItem(_itemTypeDefinition, item))
                {
                    return currentIndex;
                }
            }

            return -1;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumeratorImpl();

        private bool IsMatchingItem(TCached cachedItem, T item)
        {
            if (MSBuildNameIgnoreCaseComparer.Default.Equals(cachedItem.Key, item.Key))
            {
                T? foundItem = _getInstance(cachedItem);
                if (foundItem is not null && foundItem.Equals(item))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator<T> GetEnumeratorImpl()
        {
            if (_itemList != null)
            {
                foreach (var item in _itemList)
                {
                    T? instance = _getInstance(item);
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }

            if (_itemTypeDefinition != null)
            {
                T? instance = _getInstance(_itemTypeDefinition);
                if (instance != null)
                {
                    yield return instance;
                }
            }
        }
    }
}
