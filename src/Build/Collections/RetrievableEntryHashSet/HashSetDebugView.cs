// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Debug view for HashSet
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class HashSetDebugView<T> where T : class, IKeyed
    {
        private readonly RetrievableEntryHashSet<T> _set;

        public HashSetDebugView(RetrievableEntryHashSet<T> set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => _set.ToArray();
    }
}
