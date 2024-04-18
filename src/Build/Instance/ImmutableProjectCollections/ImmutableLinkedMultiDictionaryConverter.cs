// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Instance.ImmutableProjectCollections
{
    internal class ImmutableLinkedMultiDictionaryConverter<K, VCached, V> : IMultiDictionary<K, V>
        where K : class
        where V : class
        where VCached : class
    {
        private readonly Func<K, IEnumerable<VCached>> _getCachedValues;
        private readonly Func<VCached, V> _getInstance;

        public ImmutableLinkedMultiDictionaryConverter(Func<K, IEnumerable<VCached>> getCachedValues, Func<VCached, V> getInstance)
        {
            _getCachedValues = getCachedValues;
            _getInstance = getInstance;
        }

        public IEnumerable<V> this[K key]
        {
            get
            {
                IEnumerable<VCached> cachedValues = _getCachedValues(key);
                if (cachedValues != null)
                {
                    foreach (var cachedValue in cachedValues)
                    {
                        yield return _getInstance(cachedValue);
                    }
                }
            }
        }
    }
}
