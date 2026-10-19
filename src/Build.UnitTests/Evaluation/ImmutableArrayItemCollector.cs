// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Collects one evaluated-item output channel in an owned immutable array.
/// </summary>
/// <typeparam name="T">The type of evaluated item.</typeparam>
/// <param name="ignoreConditions">Whether to collect all items rather than only condition-true items.</param>
/// <remarks>
///  The ordered design-time list is not recorded, avoiding duplicates of condition-true items.
/// </remarks>
internal sealed class ImmutableArrayItemCollector<T>(bool ignoreConditions = true) : IItemEvaluationCollector<T>
    where T : IItem
{
    private readonly ImmutableArray<T>.Builder _items = ImmutableArray.CreateBuilder<T>();

    public bool ShouldEvaluateForDesignTime => ignoreConditions;

    public void AddItem(T item)
    {
        if (!ignoreConditions)
        {
            _items.Add(item);
        }
    }

    public void AddToAllEvaluatedItemsList(T item)
    {
    }

    public void AddItemIgnoringCondition(T item) => _items.Add(item);

    /// <summary>
    ///  Copies the collected items into an owned immutable array.
    /// </summary>
    /// <returns>
    ///  The items received through the selected output channel.
    /// </returns>
    public ImmutableArray<T> ToImmutable() => _items.ToImmutable();
}
