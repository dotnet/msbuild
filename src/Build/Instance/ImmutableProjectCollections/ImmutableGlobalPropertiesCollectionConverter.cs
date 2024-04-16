// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Instance.ImmutableProjectCollections
{
    internal class ImmutableGlobalPropertiesCollectionConverter : IRetrievableEntryHashSet<ProjectPropertyInstance>
    {
        private IDictionary<string, string> _globalProperties;
        private PropertyDictionary<ProjectPropertyInstance> _allProperties;
        private ValuesCollection _values;

        public ImmutableGlobalPropertiesCollectionConverter(
            IDictionary<string, string> globalProperties,
            PropertyDictionary<ProjectPropertyInstance> allProperties)
        {
            _globalProperties = globalProperties;
            _allProperties = allProperties;
            _values = new ValuesCollection(this);
        }

        public ProjectPropertyInstance this[string key]
        {
            set => throw new NotSupportedException();
            get
            {
                if (_globalProperties.ContainsKey(key))
                {
                    return _allProperties[key];
                }

                return null;
            }
        }

        public int Count => _globalProperties.Count;

        public bool IsReadOnly => true;

        public ICollection<string> Keys => _globalProperties.Keys;

        public ICollection<ProjectPropertyInstance> Values => _values;

        public void Add(ProjectPropertyInstance item) => throw new NotSupportedException();

        public void Add(string key, ProjectPropertyInstance value) => throw new NotSupportedException();

        public void Add(KeyValuePair<string, ProjectPropertyInstance> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(ProjectPropertyInstance item) => _values.Contains(item);

        public bool Contains(KeyValuePair<string, ProjectPropertyInstance> itemKvp) => _values.Contains(itemKvp.Value);

        public bool ContainsKey(string key) => _globalProperties.ContainsKey(key);

        public void CopyTo(ProjectPropertyInstance[] array) => _values.CopyTo(array, arrayIndex: 0);

        public void CopyTo(ProjectPropertyInstance[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

        public void CopyTo(ProjectPropertyInstance[] array, int arrayIndex, int count) => _values.CopyTo(array, arrayIndex, count);

        public void CopyTo(KeyValuePair<string, ProjectPropertyInstance>[] array, int arrayIndex)
        {
            ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _globalProperties.Count);

            int currentIndex = arrayIndex;
            foreach (var itemKey in _globalProperties.Keys)
            {
                ProjectPropertyInstance instance = _allProperties[itemKey];
                if (instance != null)
                {
                    array[currentIndex] = new KeyValuePair<string, ProjectPropertyInstance>(itemKey, instance);
                    ++currentIndex;
                }
            }
        }

        public ProjectPropertyInstance Get(string key)
        {
            if (_globalProperties.ContainsKey(key))
            {
                return null;
            }

            return _allProperties[key];
        }

        public ProjectPropertyInstance Get(string key, int index, int length)
        {
            // The PropertyDictionary containing all of the properties can efficiently
            // look up the requested property while honoring the specific index and length
            // constraints. We then just have to verify that it's one of the global properties.
            ProjectPropertyInstance actualProperty = _allProperties.Get(key, index, length);
            if (actualProperty != null && _globalProperties.ContainsKey(actualProperty.Name))
            {
                return actualProperty;
            }

            return null;
        }

        public IEnumerator<ProjectPropertyInstance> GetEnumerator() => _values.GetEnumerator();

        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotSupportedException();

        public void OnDeserialization(object sender) => throw new NotSupportedException();

        public bool Remove(ProjectPropertyInstance item) => throw new NotSupportedException();

        public bool Remove(string key) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<string, ProjectPropertyInstance> item) => throw new NotSupportedException();

        public void TrimExcess()
        {
        }

        public bool TryGetValue(string key, out ProjectPropertyInstance value)
        {
            ProjectPropertyInstance instance = Get(key);
            value = instance;
            return instance != null;
        }

        public void UnionWith(IEnumerable<ProjectPropertyInstance> other) => throw new NotSupportedException();

        IEnumerator<KeyValuePair<string, ProjectPropertyInstance>> IEnumerable<KeyValuePair<string, ProjectPropertyInstance>>.GetEnumerator()
        {
            foreach (var itemKey in _globalProperties.Keys)
            {
                ProjectPropertyInstance instance = _allProperties[itemKey];
                if (instance != null)
                {
                    yield return new KeyValuePair<string, ProjectPropertyInstance>(itemKey, instance);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

        private class ValuesCollection : ICollection<ProjectPropertyInstance>
        {
            private readonly ImmutableGlobalPropertiesCollectionConverter _parent;

            public ValuesCollection(ImmutableGlobalPropertiesCollectionConverter parent)
            {
                _parent = parent;
            }

            public int Count => _parent._globalProperties.Count;

            public bool IsReadOnly => true;

            public void Add(ProjectPropertyInstance item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(ProjectPropertyInstance item) => throw new NotSupportedException();

            public bool Contains(ProjectPropertyInstance item)
            {
                if (!_parent._globalProperties.ContainsKey(item.Name))
                {
                    return false;
                }

                ProjectPropertyInstance actualInstance = _parent._allProperties[item.Name];

                if (actualInstance == null)
                {
                    return false;
                }

                return actualInstance.Equals(item);
            }

            public void CopyTo(ProjectPropertyInstance[] array, int arrayIndex)
            {
                CopyTo(array, arrayIndex, _parent._globalProperties.Count);
            }

            public void CopyTo(ProjectPropertyInstance[] array, int arrayIndex, int count)
            {
                ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), _parent._globalProperties.Count);

                int currentIndex = arrayIndex;
                int currentCount = 0;
                foreach (var itemKey in _parent._globalProperties.Keys)
                {
                    if (currentCount >= count)
                    {
                        return;
                    }

                    ProjectPropertyInstance instance = _parent._allProperties[itemKey];
                    if (instance != null)
                    {
                        array[currentIndex] = instance;
                        ++currentIndex;
                        ++currentCount;
                    }
                }
            }

            public IEnumerator<ProjectPropertyInstance> GetEnumerator()
            {
                foreach (var itemKey in _parent._globalProperties.Keys)
                {
                    ProjectPropertyInstance instance = _parent._allProperties[itemKey];
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                foreach (var itemKey in _parent._globalProperties.Keys)
                {
                    ProjectPropertyInstance instance = _parent._allProperties[itemKey];
                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }
        }
    }
}
