// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Collects evaluated items and optional design-time item output.
/// </summary>
/// <typeparam name="T">The type of evaluated item.</typeparam>
internal interface IItemEvaluationCollector<T>
    where T : IItem
{
    /// <summary>
    ///  Gets whether evaluation should collect design-time information in addition to normal build inputs.
    /// </summary>
    bool ShouldEvaluateForDesignTime { get; }

    /// <summary>
    ///  Adds an item whose originating conditions evaluated to <see langword="true"/>.
    /// </summary>
    /// <param name="item">The evaluated item.</param>
    void AddItem(T item);

    /// <summary>
    ///  Adds a condition-true item to the ordered design-time list of evaluated items.
    /// </summary>
    /// <param name="item">The evaluated item.</param>
    void AddToAllEvaluatedItemsList(T item);

    /// <summary>
    ///  Adds an item to the design-time collection that ignores originating conditions.
    /// </summary>
    /// <param name="item">The evaluated item.</param>
    void AddItemIgnoringCondition(T item);
}
