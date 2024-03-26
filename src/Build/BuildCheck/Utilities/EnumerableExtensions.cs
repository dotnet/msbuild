// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Experimental.BuildCheck;

internal static class EnumerableExtensions
{
    /// <summary>
    /// Concatenates items of input sequence into csv string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">Sequence to be turned into csv string.</param>
    /// <param name="useSpace">Indicates whether space should be inserted between comas and following items.</param>
    /// <returns>Csv string.</returns>
    public static string ToCsvString<T>(this IEnumerable<T>? source, bool useSpace = true)
    {
        return source == null ? "<NULL>" : string.Join("," + (useSpace ? " " : string.Empty), source);
    }

    /// <summary>
    /// Performs an action for each element in given sequence.
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
    {
        foreach (T element in sequence)
        {
            action(element);
        }
    }

    /// <summary>
    /// Adds a content of given dictionary to current dictionary.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="dict">Dictionary to receive another values.</param>
    /// <param name="another">Dictionary to be merged into current.</param>
    /// <param name="mergeValues">Way of resolving keys conflicts.</param>
    public static void Merge<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        IReadOnlyDictionary<TKey, TValue> another,
        Func<TValue, TValue, TValue> mergeValues)
    {
        foreach (var pair in another)
        {
            if (!dict.ContainsKey(pair.Key))
            {
                dict[pair.Key] = pair.Value;
            }
            else
            {
                dict[pair.Key] = mergeValues(dict[pair.Key], pair.Value);
            }
        }
    }
}
