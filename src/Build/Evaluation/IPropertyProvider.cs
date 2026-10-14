// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Provides properties to an <see cref="Expander{P, I}"/>.
/// </summary>
/// <typeparam name="T">The type of property provided.</typeparam>
internal interface IPropertyProvider<out T>
    where T : class
{
    /// <summary>
    ///  Gets the property with the specified name.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>
    ///  The property, or <see langword="null"/> if no property has the specified name.
    /// </returns>
    T? GetProperty(string name);

    /// <summary>
    ///  Gets the property whose name is the specified segment of a string.
    /// </summary>
    /// <param name="name">The string containing the property name.</param>
    /// <param name="startIndex">The zero-based index of the first character of the property name.</param>
    /// <param name="endIndex">The zero-based index of the last character of the property name.</param>
    /// <returns>
    ///  The property, or <see langword="null"/> if no property has the specified name.
    /// </returns>
    T? GetProperty(string name, int startIndex, int endIndex);
}
