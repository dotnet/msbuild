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
        ///  Gets the value whose key is the specified segment of a string.
        /// </summary>
        /// <param name="keyString">A string that contains the key of the item to retrieve.</param>
        /// <param name="startIndex">The zero-based index of the first character of the key.</param>
        /// <param name="length">The number of characters in the key.</param>
        /// <returns>
        ///  The value, or <see langword="null"/> if the dictionary does not contain the specified key.
        /// </returns>
        TValue? Get(string keyString, int startIndex, int length);
    }
}
