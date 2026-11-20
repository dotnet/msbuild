// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

/// <summary>
///  Lock-free views over the keys or values of a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
///  <see cref="ConcurrentDictionary{TKey, TValue}.Keys"/> and <see cref="ConcurrentDictionary{TKey, TValue}.Values"/>
///  take every lock and copy the contents. These enumerate the dictionary directly, which is lock-free and reflects
///  concurrent changes.
/// </remarks>
internal static class ConcurrentDictionaryExtensions
{
    public static KeyEnumerable<TKey, TValue> EnumerateKeys<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary)
        where TKey : notnull
        => new(dictionary);

    public static ValueEnumerable<TKey, TValue> EnumerateValues<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary)
        where TKey : notnull
        => new(dictionary);

    public readonly struct KeyEnumerable<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary) : IEnumerable<TKey>
        where TKey : notnull
    {
        public Enumerator GetEnumerator() => new(dictionary.GetEnumerator());

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator) : IEnumerator<TKey>
        {
            public readonly TKey Current => enumerator.Current.Key;

            readonly object? IEnumerator.Current => Current;

            public bool MoveNext() => enumerator.MoveNext();

            public void Reset() => enumerator.Reset();

            public void Dispose() => enumerator.Dispose();
        }
    }

    public readonly struct ValueEnumerable<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary) : IEnumerable<TValue>
        where TKey : notnull
    {
        public Enumerator GetEnumerator() => new(dictionary.GetEnumerator());

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator) : IEnumerator<TValue>
        {
            public readonly TValue Current => enumerator.Current.Value;

            readonly object? IEnumerator.Current => Current;

            public bool MoveNext() => enumerator.MoveNext();

            public void Reset() => enumerator.Reset();

            public void Dispose() => enumerator.Dispose();
        }
    }
}
