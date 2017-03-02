// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Debug view for HashSet
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class HashSetDebugView<T> where T : class, IKeyed
    {
        private RetrievableEntryHashSet<T> _set;

        public HashSetDebugView(RetrievableEntryHashSet<T> set)
        {
            if (set == null)
            {
                throw new ArgumentNullException("set");
            }

            _set = set;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                return _set.ToArray();
            }
        }
    }
}
