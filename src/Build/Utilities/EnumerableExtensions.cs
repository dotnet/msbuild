// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Internal;

internal static class EnumerableExtensions
{
    public static void CopyTo<T>(this IReadOnlyList<T> list, T[] array, int startIndex)
    {
        for (int i = 0, count = list.Count; i < count; i++)
        {
            array[startIndex + i] = list[i];
        }
    }
}
