// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    /// <summary>
    /// A specialized collection used when item data originates in an immutable Project.
    /// </summary>
    internal sealed class ImmutableItemDictionary<TCached, T> : IItemDictionary<T>
        where T : class, IKeyed, IItem
    {
        private readonly IDictionary<string, ICollection<TCached>> _itemsByType;
        private readonly ICollection<T> _allItems;

        public ImmutableItemDictionary(IDictionary<string, ICollection<TCached>> itemsByType, ICollection<TCached> allItems)
        {
            _itemsByType = itemsByType ?? throw new ArgumentNullException(nameof(itemsByType));

            if (allItems == null)
            {
                throw new ArgumentNullException(nameof(allItems));
            }

            var convertedItems = new HashSet<T>(allItems.Count);
            foreach (var item in allItems)
            {
                T? instance = GetInstance(item);
                if (instance != null)
                {
                    convertedItems.Add(instance);
                }
            }
            _allItems = new ReadOnlyCollection<T>(convertedItems);
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

                return new ListConverter(itemType, _allItems, list);
            }
        }

        /// <inheritdoc />
        public int Count => _allItems.Count;

        /// <inheritdoc />
        public ICollection<string> ItemTypes => _itemsByType.Keys;

        /// <inheritdoc />
        public void Add(T projectItem) => throw new NotSupportedException();

        /// <inheritdoc />
        public void AddEmptyMarker(string itemType) => throw new NotSupportedException();

        /// <inheritdoc />
        public void AddRange(IEnumerable<T> projectItems) => throw new NotSupportedException();

        /// <inheritdoc />
        public void Clear() => throw new NotSupportedException();

        /// <inheritdoc />
        public bool Contains(T projectItem) => _allItems.Contains(projectItem);

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

                itemTypeCallback(kvp.Key, new ListConverter(kvp.Key, _allItems, kvp.Value));
            }
        }

        /// <inheritdoc />
        public IEnumerable<TResult> GetCopyOnReadEnumerable<TResult>(Func<T, TResult> selector)
        {
            foreach (var item in _allItems)
            {
                yield return selector(item);
            }
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => _allItems.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => _allItems.GetEnumerator();

        /// <inheritdoc />
        public ICollection<T> GetItems(string itemType)
        {
            if (_itemsByType.TryGetValue(itemType, out ICollection<TCached>? items))
            {
                return new ListConverter(itemType, _allItems, items);
            }

            return Array.Empty<T>();
        }

        /// <inheritdoc />
        public bool HasEmptyMarker(string itemType) => _itemsByType.Values.Any(list => list.Count == 0);

        /// <inheritdoc />
        public void ImportItems(IEnumerable<T> other) => throw new NotSupportedException();

        /// <inheritdoc />
        public void ImportItemsOfType(string itemType, IEnumerable<T> items) => throw new NotSupportedException();

        /// <inheritdoc />
        public bool Remove(T projectItem) => throw new NotSupportedException();

        /// <inheritdoc />
        public void RemoveItems(IEnumerable<T> other) => throw new NotSupportedException();

        /// <inheritdoc />
        public void Replace(T existingItem, T newItem) => throw new NotSupportedException();

        private static T? GetInstance(TCached item)
        {
            if (item is IImmutableInstanceProvider<T> instanceProvider)
            {
                return instanceProvider.ImmutableInstance;
            }

            return null;
        }

        private sealed class ListConverter : ICollection<T>
        {
            private readonly string _itemType;
            private readonly ICollection<T> _allItems;
            private readonly ICollection<TCached> _list;

            public ListConverter(string itemType, ICollection<T> allItems, ICollection<TCached> list)
            {
                _itemType = itemType;
                _allItems = allItems;
                _list = list;
            }

            public int Count => _list.Count;

            public bool IsReadOnly => true;

            public void Add(T item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(T item) => throw new NotSupportedException();

            public bool Contains(T item)
            {
                return MSBuildNameIgnoreCaseComparer.Default.Equals(item.Key, _itemType) &&
                       _allItems.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _list.Count);

                int currentIndex = arrayIndex;
                foreach (var item in _list)
                {
                    T? instance = GetInstance(item);
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
                    T? instance = GetInstance(item);
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
                    T? instance = GetInstance(item);
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }
        }
    }
}
