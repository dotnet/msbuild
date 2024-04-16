// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Collections
{
    internal interface IRetrievableValuedEntryHashSet<T> : IRetrievableEntryHashSet<T>
        where T : class, IKeyed, IValued
    {
        /// <summary>
        /// Gets the <see cref="IValued.EscapedValue"/> of the item whose <see cref="IKeyed.Key"/> matches <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key of the item whose value is sought.</param>
        /// <param name="escapedValue">The out parameter by which a successfully retrieved <see cref="IValued.EscapedValue"/> is returned.</param>
        /// <returns>True if an item whose <see cref="IKeyed.Key"/> matches <paramref name="key"/> was found. False otherwise.</returns>
        bool TryGetEscapedValue(string key, out string escapedValue);
    }
}
