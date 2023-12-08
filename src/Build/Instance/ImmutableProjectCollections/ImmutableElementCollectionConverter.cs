// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Runtime.Serialization;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    /// <summary>
    /// A specialized collection used when element data originates in an immutable Project.
    /// </summary>
    internal sealed class ImmutableElementCollectionConverter<TCached, T> : IRetrievableEntryHashSet<T>
        where T : class, IKeyed
    {
        private readonly IDictionary<string, TCached> _projectElements;
        private readonly IDictionary<(string, int, int), TCached> _constrainedProjectElements;
        private readonly ValuesCollection _values;

        public ImmutableElementCollectionConverter(
            IDictionary<string, TCached> projectElements,
            IDictionary<(string, int, int), TCached> constrainedProjectElements)
        {
            _projectElements = projectElements;
            _constrainedProjectElements = constrainedProjectElements;
            _values = new ValuesCollection(_projectElements, _constrainedProjectElements);
        }

        public T this[string key]
        {
            get => Get(key);
            set => throw new NotSupportedException();
        }

        public int Count => _values.Count;

        public bool IsReadOnly => true;

        public ICollection<string> Keys => _projectElements.Keys;

        public ICollection<T> Values => _values;

        public void Add(T item) => throw new NotSupportedException();

        public void Add(string key, T value) => throw new NotSupportedException();

        public void Add(KeyValuePair<string, T> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(T item) => _projectElements.ContainsKey(item.Key);

        // Note: This implementation of Contains(KeyValuePair<string, T> only checks whether the collection contains
        // an item with the same key. This doesn't match the general behavior of collection comparison, where the
        // KeyValuePair's key *and* value are compared. This is done intentionally in order to match the behavior of
        // RetrievableEntryHashSet, which only checks for the existence of an item with the same key (ignoring
        // whether the values match).
        public bool Contains(KeyValuePair<string, T> item) => _projectElements.ContainsKey(item.Key);

        public bool ContainsKey(string key) => _projectElements.ContainsKey(key);

        public void CopyTo(T[] array) => _values.CopyTo(array, arrayIndex: 0);

        public void CopyTo(T[] array, int arrayIndex, int count) => _values.CopyTo(array, arrayIndex, count);

        public void CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

        public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

        public T Get(string key) => _values.Get(key);

        public T Get(string key, int index, int length) => _values.Get(key, index, length);

        public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();

        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotSupportedException();

        public void OnDeserialization(object sender) => throw new NotSupportedException();

        public bool Remove(T item) => throw new NotSupportedException();

        public bool Remove(string key) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<string, T> item) => throw new NotSupportedException();

        public void TrimExcess()
        {
        }

        public bool TryGetValue(string key, out T value) => _values.TryGetValue(key, out value);

        public void UnionWith(IEnumerable<T> other) => throw new NotSupportedException();

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator() => _values.GetKvpEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

        /// <summary>
        /// Wraps the Project's values.
        /// </summary>
        private sealed class ValuesCollection : ICollection<T>
        {
            private readonly IDictionary<string, TCached> _projectElements;
            private readonly IDictionary<(string, int, int), TCached> _constrainedProjectElements;

            public ValuesCollection(
                IDictionary<string, TCached> projectElements,
                IDictionary<(string, int, int), TCached> constrainedProjectElements)
            {
                _projectElements = projectElements;
                _constrainedProjectElements = constrainedProjectElements;
            }

            public int Count => _projectElements.Count;

            public bool IsReadOnly => true;

            public void Add(T item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(T item)
            {
                if (item == null)
                {
                    throw new ArgumentNullException(nameof(item));
                }

                return _projectElements.ContainsKey(item.Key);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                CopyTo(array, arrayIndex, _projectElements.Count);
            }

            public void CopyTo(T[] array, int arrayIndex, int count)
            {
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), count);

                int index = arrayIndex;
                int endIndex = arrayIndex + count;
                foreach (var item in _projectElements.Values)
                {
                    array[index] = GetElementInstance(item);
                    ++index;
                    if (index >= endIndex)
                    {
                        break;
                    }
                }
            }

            public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
            {
                ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _projectElements.Count);

                int index = arrayIndex;
                foreach (var item in _projectElements.Values)
                {
                    var itemInstance = GetElementInstance(item);
                    array[index] = new KeyValuePair<string, T>(itemInstance.Key, itemInstance);
                    ++index;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (var item in _projectElements.Values)
                {
                    yield return GetElementInstance(item);
                }
            }

            public IEnumerator<KeyValuePair<string, T>> GetKvpEnumerator()
            {
                foreach (var kvp in _projectElements)
                {
                    T instance = GetElementInstance(kvp.Value);
                    yield return new KeyValuePair<string, T>(kvp.Key, instance);
                }
            }

            public bool Remove(T item) => throw new NotSupportedException();

            IEnumerator IEnumerable.GetEnumerator()
            {
                foreach (var item in _projectElements.Values)
                {
                    yield return GetElementInstance(item);
                }
            }

            public T Get(string key)
            {
                if (_projectElements.TryGetValue(key, out TCached element))
                {
                    return GetElementInstance(element);
                }

                return null;
            }

            public T Get(string keyString, int startIndex, int length)
            {
                if (_constrainedProjectElements.TryGetValue((keyString, startIndex, length), out TCached element))
                {
                    return GetElementInstance(element);
                }

                return null;
            }

            public bool TryGetValue(string key, out T value)
            {
                value = null;
                if (!_projectElements.TryGetValue(key, out TCached element))
                {
                    return false;
                }

                value = GetElementInstance(element);
                return value != null;
            }

            private T GetElementInstance(TCached element)
            {
                if (element is IImmutableInstanceProvider<T> instanceProvider)
                {
                    return instanceProvider.ImmutableInstance;
                }

                return null;
            }
        }
    }
}
