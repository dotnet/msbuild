// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

/// <summary>
/// Helper extension methods for working with data passed via
/// <see cref="ProjectEvaluationFinishedEventArgs"/> and <see cref="ProjectStartedEventArgs"/>
/// </summary>
public static class BuildEventArgsExtensions
{
    /// <summary>
    /// Lazy enumerates and strong types properties from Properties property.
    /// </summary>
    public static IEnumerable<(string propertyName, string propertyValue)> EnumerateProperties(
        this ProjectEvaluationFinishedEventArgs eventArgs)
        => EnumerateProperties(eventArgs.Properties);

    /// <summary>
    /// Lazy enumerates and strong types properties from Properties property.
    /// </summary>
    public static IEnumerable<(string propertyName, string propertyValue)> EnumerateProperties(
        this ProjectStartedEventArgs eventArgs)
        => EnumerateProperties(eventArgs.Properties);

    /// <summary>
    /// Lazy enumerates and partially strong types items from Items property.
    /// The actual item value is of nongeneric <see cref="object"/> type.
    /// The actual type need to be inferred during runtime based on the itemType.
    /// </summary>
    public static IEnumerable<(string itemType, IItem itemValue)> EnumerateItems(
        this ProjectEvaluationFinishedEventArgs eventArgs)
        => EnumerateItems(eventArgs.Items);

    /// <summary>
    /// Lazy enumerates and partially strong types items from Items property.
    /// The actual item value is of nongeneric <see cref="object"/> type.
    /// The actual type need to be inferred during runtime based on the itemType.
    /// </summary>
    public static IEnumerable<(string itemType, IItem itemValue)> EnumerateItems(
        this ProjectStartedEventArgs eventArgs)
        => EnumerateItems(eventArgs.Items);

    private static IEnumerable<(string propertyName, string propertyValue)> EnumerateProperties(IEnumerable? properties)
        => Internal.Utilities.EnumerateProperties(properties);

    private static IEnumerable<(string itemType, IItem itemValue)> EnumerateItems(IEnumerable? items)
        => Internal.Utilities.EnumerateItems(items);
}
