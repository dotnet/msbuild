// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    /// <summary>
    /// A specialized collection used when item data originates in an immutable Project.
    /// </summary>
    internal sealed class ImmutableItemDictionary<TCached, T> : IItemDictionary<T>
        where T : class, IKeyed, IItem
        where TCached : IKeyed, IItem
    {
        private readonly IDictionary<string, ICollection<TCached>> _itemsByType;
        private readonly ICollection<TCached> _allCachedItems;
        private readonly Func<TCached, T?> _getInstance;

        public ImmutableItemDictionary(
            ICollection<TCached> allItems,
            IDictionary<string, ICollection<TCached>> itemsByType,
            Func<TCached, T?> getInstance,
            Func<T, string?> getItemType)
        {
            if (allItems == null)
            {
                throw new ArgumentNullException(nameof(allItems));
            }

            _allCachedItems = allItems;
            _itemsByType = itemsByType ?? throw new ArgumentNullException(nameof(itemsByType));
            _getInstance = getInstance;
        }

        /// <inheritdoc />
        public ICollection<T> this[string itemType]
        {
            get
            {
                if (!_itemsByType.TryGetValue(itemType, out ICollection<TCached>? list))
                {
                    return Array.Empty<T>();
                }

                return new ListConverter(itemType, list, _getInstance);
            }
        }

        /// <inheritdoc />
        public int Count => _allCachedItems.Count;

        /// <inheritdoc />
        public ICollection<string> ItemTypes => _itemsByType.Keys;

        /// <inheritdoc />
        public void Add(T projectItem) => throw new NotSupportedException();

        /// <inheritdoc />
        public void Clear() => throw new NotSupportedException();

        /// <inheritdoc />
        public void EnumerateItemsPerType(Action<string, IEnumerable<T>> itemTypeCallback)
        {
            foreach (var kvp in _itemsByType)
            {
                if (kvp.Value == null || kvp.Value.Count == 0)
                {
                    // skip empty markers
                    continue;
                }

                itemTypeCallback(kvp.Key, new ListConverter(kvp.Key, kvp.Value, _getInstance));
            }
        }

        /// <inheritdoc />
        public IEnumerable<TResult> GetCopyOnReadEnumerable<TResult>(Func<T, TResult> selector)
        {
            foreach (var cachedItem in _allCachedItems)
            {
                T? item = _getInstance(cachedItem);
                if (item is not null)
                {
                    yield return selector(item);
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            foreach (var cachedItem in _allCachedItems)
            {
                T? item = _getInstance(cachedItem);
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var cachedItem in _allCachedItems)
            {
                T? item = _getInstance(cachedItem);
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc />
        public ICollection<T> GetItems(string itemType)
        {
            if (_itemsByType.TryGetValue(itemType, out ICollection<TCached>? items))
            {
                return new ListConverter(itemType, items, _getInstance);
            }

            return Array.Empty<T>();
        }

        /// <inheritdoc />
        public void ImportItems(IEnumerable<T> other) => throw new NotSupportedException();

        /// <inheritdoc />
        public void ImportItemsOfType(string itemType, IEnumerable<T> items) => throw new NotSupportedException();

        /// <inheritdoc />
        public bool Remove(T projectItem) => throw new NotSupportedException();

        /// <inheritdoc />
        public void RemoveItemsOfType(string itemType, IEnumerable<T> other) => throw new NotSupportedException();

        private sealed class ListConverter : ICollection<T>
        {
            private readonly ICollection<TCached> _list;
            private readonly Func<TCached, T?> _getInstance;

            public ListConverter(string itemType, ICollection<TCached> list, Func<TCached, T?> getInstance)
            {
                _list = list;
                _getInstance = getInstance;
            }

            public int Count => _list.Count;

            public bool IsReadOnly => true;

            public void Add(T item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(T item) => throw new NotSupportedException();

            public bool Contains(T item)
            {
                return _list.Any(
                    cachedItem =>
                    {
                        if (MSBuildNameIgnoreCaseComparer.Default.Equals(cachedItem.EvaluatedIncludeEscaped, item.EvaluatedIncludeEscaped))
                        {
                            T? foundItem = _getInstance(cachedItem);
                            if (foundItem is not null && foundItem.Equals(item))
                            {
                                return true;
                            }
                        }

                        return false;
                    });
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _list.Count);

                int currentIndex = arrayIndex;
                foreach (var item in _list)
                {
                    T? instance = _getInstance(item);
                    if (instance != null)
                    {
                        array[currentIndex] = instance;
                        ++currentIndex;
                    }
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (var item in _list)
                {
                    T? instance = _getInstance(item);
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                foreach (var item in _list)
                {
                    T? instance = _getInstance(item);
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }
        }
    }
}
