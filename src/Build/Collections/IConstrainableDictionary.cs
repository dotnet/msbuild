// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Represents an <see cref="IDictionary{String, TValue}"/> that supports use of an
    /// <see cref="IConstrainedEqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the values in the dictionary. The key is assumed
    /// to always be <see cref="String"/>.</typeparam>
    internal interface IConstrainableDictionary<TValue> : IDictionary<string, TValue>
    {
        /// <summary>
        /// Get the value with the specified key or null if it is not present.
        /// The key used for lookup is the substring of <paramref name="keyString"/>
        /// starting at <paramref name="startIndex"/> and ending at <paramref name="endIndex"/>
        /// (e.g. if the key is just the first character in <paramref name="keyString"/>, then
        /// the value for <paramref name="startIndex"/> should be 0 and the value for
        /// <paramref name="endIndex"/> should also be 0.)
        /// </summary>
        /// <param name="keyString">A string that contains the key of the item to retrieve.</param>
        /// <param name="startIndex">The start index of the substring of <paramref name="keyString"/> that contains the key.</param>
        /// <param name="endIndex">The end index of the substring of <paramref name="keyString"/> that contains the key.</param>
        /// <returns>If it's found, the item whose key matches the calculated substring. Null otherwise.</returns>
        TValue? Get(string keyString, int startIndex, int endIndex);
    }
}
