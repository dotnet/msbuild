// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Like <see cref="IRetrievableEntryHashSet{T}"/>, this represents a hash set mapping string to <typeparamref name="T"/>
    /// with the specialization that value lookup supports using substrings of a provided key without requiring instantiating
    /// the substring (in order to avoid the memory usage of string allocation).
    ///
    /// This interface extends the functionality of <see cref="IRetrievableEntryHashSet{T}"/> by introducing the ability
    /// to directly retrieve the Value of an instance of T instead of retrieving the instance of T itself. Implementations of
    /// this interface can avoid the cost of allocating an instance of <typeparamref name="T"/> when the caller requests only
    /// the Value.
    /// </summary>
    /// <typeparam name="T">The type of data the hash set contains (which must be
    /// <see cref="IKeyed"/> and also <see cref="IValued"/>).</typeparam>
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
