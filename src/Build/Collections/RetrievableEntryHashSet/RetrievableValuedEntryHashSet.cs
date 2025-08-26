// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <inheritdoc />
    [DebuggerTypeProxy(typeof(HashSetDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
#if FEATURE_SECURITY_PERMISSIONS
    [System.Security.Permissions.HostProtection(MayLeakOnAbort = true)]
#endif
    internal class RetrievableValuedEntryHashSet<T> : RetrievableEntryHashSet<T>, IRetrievableValuedEntryHashSet<T>
        where T : class, IKeyed, IValued
    {
        /// <summary>
        /// Initializes a new instance of the RetrievableValuedEntryHashSet class.
        /// </summary>
        /// <param name="comparer">A comparer with which the items' <see cref="IKeyed.Key"/> key values are compared.</param>
        public RetrievableValuedEntryHashSet(IEqualityComparer<string> comparer)
            : base(comparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the RetrievableValuedEntryHashSet class.
        /// </summary>
        /// <param name="suggestedCapacity">A value suggesting a good approximate minimum size for the initial collection.</param>
        /// <param name="comparer">A comparer with which the items' <see cref="IKeyed.Key"/> key values are compared.</param>
        public RetrievableValuedEntryHashSet(int suggestedCapacity, IEqualityComparer<string> comparer)
            : base(suggestedCapacity, comparer)
        {
        }

        /// <inheritdoc />
        public bool TryGetEscapedValue(string key, out string escapedValue)
        {
            if (TryGetValue(key, out T item) && item != null)
            {
                escapedValue = item.EscapedValue;
                return true;
            }

            escapedValue = null;
            return false;
        }
    }
}
