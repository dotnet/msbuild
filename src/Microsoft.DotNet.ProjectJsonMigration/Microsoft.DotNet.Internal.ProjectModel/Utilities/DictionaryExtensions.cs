// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Internal.ProjectModel.Utilities
{
    internal static class DictionaryExtensions
    {
        public static IEnumerable<V> GetOrEmpty<K, V>(this IDictionary<K, IEnumerable<V>> self, K key)
        {
            IEnumerable<V> val;
            return !self.TryGetValue(key, out val) 
                ? Enumerable.Empty<V>() 
                : val;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
        {
            lock (dictionary)
            {
                TValue value;
                if (!dictionary.TryGetValue(key, out value))
                {
                    value = factory(key);
                    dictionary[key] = value;
                }

                return value;
            }
        }
    }
}
