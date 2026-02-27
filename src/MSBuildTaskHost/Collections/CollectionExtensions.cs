// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.TaskHost.Collections;

internal static class CollectionExtensions
{
    /// <summary>
    ///  Checks whether the dictionary contains the specified key with a value that matches the given
    ///  value using the specified string comparison.
    /// </summary>
    /// <param name="dictionary">The dictionary to check.</param>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <param name="value">The value to compare against the dictionary's value.</param>
    /// <param name="comparison">The string comparison method to use.</param>
    /// <returns>
    ///  <see langword="true"/> if the dictionary contains the key and its associated value equals
    ///  the specified value; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool HasValue(
        this Dictionary<string, string?> dictionary,
        string key,
        string? value,
        StringComparison comparison)
        => dictionary.TryGetValue(key, out string? existingValue)
            && string.Equals(value, existingValue, comparison);
}
