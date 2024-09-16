﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Build.Experimental.BuildCheck;

internal static class EnumerableExtensions
{
    /// <summary>
    /// Concatenates items of input sequence into csv string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">Sequence to be turned into csv string.</param>
    /// <param name="useSpace">Indicates whether space should be inserted between commas and following items.</param>
    /// <returns>Csv string.</returns>
    public static string ToCsvString<T>(this IEnumerable<T>? source, bool useSpace = true)
    {
        return source == null ? "<NULL>" : string.Join(useSpace ? ", " : ",", source);
    }

    /// <summary>
    /// Returns the item as an enumerable with single item.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="item"></param>
    /// <returns></returns>
    public static IEnumerable<T> AsSingleItemEnumerable<T>(this T item)
    {
        yield return item;
    }

    public static HashSet<T> NewHashSet<T>(int capacity)
        => NewHashSet<T>(capacity, null);

    public static HashSet<T> NewHashSet<T>(IEqualityComparer<T> equalityComparer)
        => NewHashSet<T>(0, equalityComparer);

    public static HashSet<T> NewHashSet<T>(int capacity, IEqualityComparer<T>? equalityComparer)
    {
#if NETSTANDARD2_0
        return new HashSet<T>(equalityComparer);
#else
        return new HashSet<T>(capacity, equalityComparer);
#endif
    }

    public static HashSet<T>? ToHashSet<T>(this ICollection<T>? source, IEqualityComparer<T>? equalityComparer = null)
    {
        if (source is null)
        {
            return null;
        }

        if (source is HashSet<T> set)
        {
            return set;
        }

        return new HashSet<T>(source, equalityComparer);
    }

#if !NET
    /// <summary>
    /// Returns a read-only <see cref="ReadOnlyDictionary{TKey, TValue}"/> wrapper
    /// for the current dictionary.
    /// </summary>
    public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        => new(dictionary);
#endif

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
            if (!dict.TryGetValue(pair.Key, out TValue? value))
            {
                dict[pair.Key] = pair.Value;
            }
            else
            {
                dict[pair.Key] = mergeValues(value, pair.Value);
            }
        }
    }

    /// <summary>
    /// Adds a content of given list to current dictionary.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="dict">Dictionary to receive another values.</param>
    /// <param name="another">List to be merged into current.</param>
    /// <param name="extractKey">Way of getting a key of an incoming value.</param>
    /// <param name="mergeValues">Way of resolving keys conflicts.</param>
    public static void Merge<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        IReadOnlyList<TValue> another,
        Func<TValue, TKey> extractKey,
        Func<TValue, TValue, TValue> mergeValues)
    {
        foreach (var mergeValue in another)
        {
            TKey key = extractKey(mergeValue);
            if (!dict.TryGetValue(key, out TValue? value))
            {
                dict[key] = mergeValue;
            }
            else
            {
                dict[key] = mergeValues(value, mergeValue);
            }
        }
    }
}
