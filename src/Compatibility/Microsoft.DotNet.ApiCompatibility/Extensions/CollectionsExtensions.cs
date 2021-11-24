// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Extensions
{
    internal static class CollectionsExtensions
    {
        internal static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            foreach (T item in range)
                collection.Add(item);
        }
    }
}
