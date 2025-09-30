// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// This represents a specialized property dictionary which can provide a read-only dictionary without additional allocations.
    /// </summary>
    internal interface IValueDictionaryConverter
    {
        /// <summary>
        /// Gets a read-only dictionary representation of the values in this collection.
        /// </summary>
        /// <returns>A read-only dictionary.</returns>
        IDictionary<string, string> ToReadOnlyDictionary();
    }
}
