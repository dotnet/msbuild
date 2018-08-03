// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

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
