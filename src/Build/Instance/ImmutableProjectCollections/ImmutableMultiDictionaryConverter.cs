// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Instance.ImmutableProjectCollections
{
    internal class ImmutableMultiDictionaryConverter<K, VCached, V> : IMultiDictionary<K, V>
        where K : class
        where V : class
        where VCached : class
    {
        private readonly IMultiDictionary<K, VCached> _multiDictionary;
        private readonly Func<VCached, V> _getInstance;

        public ImmutableMultiDictionaryConverter(IMultiDictionary<K, VCached> multiDictionary, Func<VCached, V> getInstance)
        {
            _multiDictionary = multiDictionary;
            _getInstance = getInstance;
        }

        public IEnumerable<V> this[K key]
        {
            get
            {
                IEnumerable<VCached> cachedValues = _multiDictionary[key];
                foreach (var cachedValue in cachedValues)
                {
                    yield return _getInstance(cachedValue);
                }
            }
        }
    }
}
