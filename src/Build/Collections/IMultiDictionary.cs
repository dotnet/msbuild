// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Represents a dictionary that can hold more than one distinct value with the same key.
    /// All keys must have at least one value: null values are currently rejected.
    /// </summary>
    /// <typeparam name="K">Type of key</typeparam>
    /// <typeparam name="V">Type of value</typeparam>
    internal interface IMultiDictionary<K, V>
        where K : class
        where V : class
    {
        IEnumerable<V> this[K key] { get; }
    }
}
