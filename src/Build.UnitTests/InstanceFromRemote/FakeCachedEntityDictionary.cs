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
    /// <summary>
    /// this is a fake implementation of CachedEntityDictionary just to be used in unit tests.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <remarks>
    ///  This class deliberately does not implement any functionality except Count property.
    ///  This is to ensure that the project instance created from cache state does not access any of the collections during its construction.
    ///  We try to maintain the instance in this state working like a shim layer, and keep the cost minimal until data is actually accessed.
    /// </remarks>
    internal class FakeCachedEntityDictionary<T> :
        ICollection<T>,
        IDictionary<string, T>,
        IDictionary<(string, int, int), T>
        where T : class
    {
        public FakeCachedEntityDictionary(int count = 10)
        {
            Count = count;
        }

        public T this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public T this[(string, int, int) key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Count { get; }

        public bool IsReadOnly => throw new NotImplementedException();

        ICollection<string> IDictionary<string, T>.Keys => throw new NotImplementedException();

        ICollection<(string, int, int)> IDictionary<(string, int, int), T>.Keys => throw new NotImplementedException();

        ICollection<T> IDictionary<string, T>.Values => throw new NotImplementedException();

        ICollection<T> IDictionary<(string, int, int), T>.Values => throw new NotImplementedException();

        int ICollection<KeyValuePair<string, T>>.Count => Count;

        int ICollection<KeyValuePair<(string, int, int), T>>.Count => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, T>>.IsReadOnly => throw new NotImplementedException();

        bool ICollection<KeyValuePair<(string, int, int), T>>.IsReadOnly => throw new NotImplementedException();

        public void Add(T item) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();

        public bool Contains(T item) => throw new NotImplementedException();

        public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();

        public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();

        public bool Remove(T item) => throw new NotImplementedException();

        void IDictionary<string, T>.Add(string key, T value) => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item) => throw new NotImplementedException();

        void IDictionary<(string, int, int), T>.Add((string, int, int) key, T value) => throw new NotImplementedException();

        void ICollection<KeyValuePair<(string, int, int), T>>.Add(KeyValuePair<(string, int, int), T> item) => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, T>>.Clear() => throw new NotImplementedException();

        void ICollection<KeyValuePair<(string, int, int), T>>.Clear() => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item) => throw new NotImplementedException();

        bool ICollection<KeyValuePair<(string, int, int), T>>.Contains(KeyValuePair<(string, int, int), T> item) => throw new NotImplementedException();

        bool IDictionary<string, T>.ContainsKey(string key) => throw new NotImplementedException();

        bool IDictionary<(string, int, int), T>.ContainsKey((string, int, int) key) => throw new NotImplementedException();

        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int arrayIndex) => throw new NotImplementedException();

        void ICollection<KeyValuePair<(string, int, int), T>>.CopyTo(KeyValuePair<(string, int, int), T>[] array, int arrayIndex) => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator() => throw new NotImplementedException();

        IEnumerator<KeyValuePair<(string, int, int), T>> IEnumerable<KeyValuePair<(string, int, int), T>>.GetEnumerator() => throw new NotImplementedException();

        bool IDictionary<string, T>.Remove(string key) => throw new NotImplementedException();

        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item) => throw new NotImplementedException();

        bool IDictionary<(string, int, int), T>.Remove((string, int, int) key) => throw new NotImplementedException();

        bool ICollection<KeyValuePair<(string, int, int), T>>.Remove(KeyValuePair<(string, int, int), T> item) => throw new NotImplementedException();

        public virtual bool TryGetValue(string key, out T value) => throw new NotImplementedException();

        bool IDictionary<(string, int, int), T>.TryGetValue((string, int, int) key, out T value) => throw new NotImplementedException();
    }
}
