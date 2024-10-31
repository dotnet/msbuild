// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
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
    public static IEnumerable<PropertyData> EnumerateProperties(
        this ProjectEvaluationFinishedEventArgs eventArgs)
        => EnumerateProperties(eventArgs.Properties);

    /// <summary>
    /// Lazy enumerates and strong types properties from Properties property.
    /// </summary>
    public static IEnumerable<PropertyData> EnumerateProperties(
        this ProjectStartedEventArgs eventArgs)
        => EnumerateProperties(eventArgs.Properties);

    /// <summary>
    /// Lazy enumerates and partially strong types items from Items property.
    /// The actual item value is proxied via accessor methods - to be able to provide defined interface
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<ItemData> EnumerateItems(
        this ProjectEvaluationFinishedEventArgs eventArgs)
        => EnumerateItems(eventArgs.Items);

    /// <summary>
    /// Lazy enumerates and partially strong types items from Items property. Only items with matching type will be returned (case-insensitive, MSBuild valid names only).
    /// The actual item value is proxied via accessor methods - to be able to provide defined interface
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<ItemData> EnumerateItemsOfType(
        this ProjectEvaluationFinishedEventArgs eventArgs, string typeName)
        => EnumerateItemsOfType(eventArgs.Items, typeName);

    /// <summary>
    /// Lazy enumerates and strong types items from Items property.
    /// The actual item value is proxied via accessor methods - to be able to provide defined interface
    /// </summary>
    public static IEnumerable<ItemData> EnumerateItems(
        this ProjectStartedEventArgs eventArgs)
        => EnumerateItems(eventArgs.Items);

    /// <summary>
    /// Lazy enumerates and partially strong types items from Items property. Only items with matching type will be returned (case-insensitive, MSBuild valid names only).
    /// The actual item value is proxied via accessor methods - to be able to provide defined interface
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<ItemData> EnumerateItemsOfType(
        this ProjectStartedEventArgs eventArgs, string typeName)
        => EnumerateItemsOfType(eventArgs.Items, typeName);

    private static IEnumerable<PropertyData> EnumerateProperties(IEnumerable? properties)
        => Internal.Utilities.EnumerateProperties(properties);

    private static IEnumerable<ItemData> EnumerateItems(IEnumerable? items)
        => Internal.Utilities.EnumerateItems(items);

    private static IEnumerable<ItemData> EnumerateItemsOfType(IEnumerable? items, string typeName)
        => Internal.Utilities.EnumerateItemsOfType(items, typeName);
}
