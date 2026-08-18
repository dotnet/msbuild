// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// This interface extends the functionality of <see cref="IRetrievableEntryHashSet{T}"/> by introducing the ability
    /// to directly retrieve the unescaped Value of an instance of T instead of retrieving the instance of T itself. Implementations of
    /// this interface can avoid the cost of allocating when the caller requests only the unescaped value.
    /// </summary>
    internal interface IRetrievableUnescapedValuedEntryHashSet
    {
        /// <summary>
        /// Gets the unescaped value of the item whose <see cref="IKeyed.Key"/> matches <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key of the item whose value is sought.</param>
        /// <param name="unescapedValue">The out parameter by which a successfully retrieved unescaped value is returned.</param>
        /// <returns>True if an item whose <see cref="IKeyed.Key"/> matches <paramref name="key"/> was found. False otherwise.</returns>
        bool TryGetUnescapedValue(string key, [NotNullWhen(returnValue: true)] out string? unescapedValue);
    }
}
