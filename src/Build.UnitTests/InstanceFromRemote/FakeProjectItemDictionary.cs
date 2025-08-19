// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    internal sealed class FakeProjectItemDictionary : ICollection<ProjectItem>, IDictionary<string, ICollection<ProjectItem>>
    {
        ICollection<ProjectItem> IDictionary<string, ICollection<ProjectItem>>.this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        int ICollection<ProjectItem>.Count => throw new NotImplementedException();

        int ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.Count => throw new NotImplementedException();

        bool ICollection<ProjectItem>.IsReadOnly => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.IsReadOnly => throw new NotImplementedException();

        ICollection<string> IDictionary<string, ICollection<ProjectItem>>.Keys => throw new NotImplementedException();

        ICollection<ICollection<ProjectItem>> IDictionary<string, ICollection<ProjectItem>>.Values => throw new NotImplementedException();

        void ICollection<ProjectItem>.Add(ProjectItem item) => throw new NotImplementedException();

        void IDictionary<string, ICollection<ProjectItem>>.Add(string key, ICollection<ProjectItem> value) => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.Add(KeyValuePair<string, ICollection<ProjectItem>> item) => throw new NotImplementedException();

        void ICollection<ProjectItem>.Clear() => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.Clear() => throw new NotImplementedException();

        bool ICollection<ProjectItem>.Contains(ProjectItem item) => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.Contains(KeyValuePair<string, ICollection<ProjectItem>> item) => throw new NotImplementedException();

        bool IDictionary<string, ICollection<ProjectItem>>.ContainsKey(string key) => throw new NotImplementedException();

        void ICollection<ProjectItem>.CopyTo(ProjectItem[] array, int arrayIndex) => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.CopyTo(KeyValuePair<string, ICollection<ProjectItem>>[] array, int arrayIndex) => throw new NotImplementedException();

        IEnumerator<ProjectItem> IEnumerable<ProjectItem>.GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        IEnumerator<KeyValuePair<string, ICollection<ProjectItem>>> IEnumerable<KeyValuePair<string, ICollection<ProjectItem>>>.GetEnumerator() => throw new NotImplementedException();

        bool ICollection<ProjectItem>.Remove(ProjectItem item) => throw new NotImplementedException();

        bool IDictionary<string, ICollection<ProjectItem>>.Remove(string key) => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, ICollection<ProjectItem>>>.Remove(KeyValuePair<string, ICollection<ProjectItem>> item) => throw new NotImplementedException();

        bool IDictionary<string, ICollection<ProjectItem>>.TryGetValue(string key, out ICollection<ProjectItem> value) => throw new NotImplementedException();
    }
}
