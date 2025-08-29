// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    /// <summary>
    /// Implements a lazy-initialized, immutable list of strings that can be used to represent project imports in the ProjectInstance
    ///  when it is created from an immutable project link.
    /// It is to reduce memory usage to create and hold the list until a consumer actually accesses it.
    /// </summary>
    internal class LazyStringValuedList : IList<string>, IReadOnlyList<string>
    {
        private readonly LockType _syncLock = new();
        private readonly ProjectLink _immutableProject;
        private readonly Func<ProjectLink, List<string>> _getStringValues;
        private List<string>? _items;

        public LazyStringValuedList(ProjectLink immutableProject, Func<ProjectLink, List<string>> getStringValues)
        {
            _immutableProject = immutableProject;
            _getStringValues = getStringValues;
        }

        public string this[int index]
        {
            set => throw new NotSupportedException();
            get => EnsureListInitialized()[index];
        }

        public int Count => EnsureListInitialized().Count;

        public bool IsReadOnly => true;

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public void Insert(int index, string item) => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public bool Contains(string item) => IndexOf(item) >= 0;

        public void CopyTo(string[] array, int arrayIndex)
        {
            var items = EnsureListInitialized();
            ErrorUtilities.VerifyCollectionCopyToArguments(array, nameof(array), arrayIndex, nameof(arrayIndex), items.Count);

            items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<string> GetEnumerator() => EnsureListInitialized().GetEnumerator();

        public int IndexOf(string item)
        {
            List<string> itemList = EnsureListInitialized();
            for (int i = 0; i < itemList.Count; ++i)
            {
                string stringValue = itemList[i];
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(stringValue, item))
                {
                    return i;
                }
            }

            return -1;
        }

        IEnumerator IEnumerable.GetEnumerator() => EnsureListInitialized().GetEnumerator();

        private List<string> EnsureListInitialized()
        {
            if (_items == null)
            {
                lock (_syncLock)
                {
                    if (_items == null)
                    {
                        _items = _getStringValues(_immutableProject);
                    }
                }
            }

            return _items;
        }
    }
}
