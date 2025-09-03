// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Bag of information for a performed property read.
/// </summary>
/// <param name="PropertyName">The portion of MSBuild script that contains the property name, that's being expanded.</param>
/// <param name="StartIndex">Start index of the property name within the <see cref="PropertyName"/> property.</param>
/// <param name="EndIndex">End index of the property name within the <see cref="PropertyName"/> property.</param>
/// <param name="ElementLocation">The xml element location in which the property expansion happened.</param>
/// <param name="IsUninitialized">Indicates whether the property was uninitialized when being expanded.</param>
/// <param name="PropertyReadContext">Evaluation context in which the property was expanded.</param>
internal readonly record struct PropertyReadInfo(
    string PropertyName,
    int StartIndex,
    int EndIndex,
    IMSBuildElementLocation ElementLocation,
    bool IsUninitialized,
    PropertyReadContext PropertyReadContext)
{
    internal PropertyReadInfo(
        string PropertyName,
        IMSBuildElementLocation ElementLocation,
        bool IsUninitialized,
        PropertyReadContext PropertyReadContext)
        : this(PropertyName, 0, PropertyName.Length - 1, ElementLocation, IsUninitialized, PropertyReadContext)
    { }
}
