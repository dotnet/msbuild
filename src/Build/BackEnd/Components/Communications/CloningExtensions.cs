// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd;

internal static class CloningExtensions
{
    public static PropertyDictionary<ProjectPropertyInstance>? DeepClone(
        this PropertyDictionary<ProjectPropertyInstance>? properties)
        => properties == null ? null : new(properties.Select<ProjectPropertyInstance, ProjectPropertyInstance>(p => p.DeepClone()));

    public static Dictionary<TKey, TValue>? DeepClone<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        IEqualityComparer<TKey> comparer) where TKey : notnull
        => dictionary.DeepClone(null, null, comparer);

    public static Dictionary<TKey, TValue>? DeepClone<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        Func<TValue, TValue> valueClone,
        IEqualityComparer<TKey> comparer) where TKey : notnull
        => dictionary.DeepClone(null, valueClone, comparer);

    public static Dictionary<TKey, TValue>? DeepClone<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        Func<TKey, TKey> keyClone,
        IEqualityComparer<TKey> comparer) where TKey : notnull
        => dictionary.DeepClone(keyClone, null, comparer);

    public static Dictionary<TKey, TValue>? DeepClone<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        Func<TKey, TKey>? keyClone,
        Func<TValue, TValue>? valueClone,
        IEqualityComparer<TKey> comparer) where TKey : notnull
        => dictionary?.ToDictionary(
        p => (keyClone ?? Identity)(p.Key),
        p => (valueClone ?? Identity)(p.Value),
        comparer);

    private static T Identity<T>(T value) => value;
}

