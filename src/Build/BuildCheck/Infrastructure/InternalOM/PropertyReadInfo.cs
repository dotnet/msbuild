// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Bag of information for a performed property read.
/// </summary>
/// <param name="PropertyName"></param>
/// <param name="StartIndex"></param>
/// <param name="EndIndex"></param>
/// <param name="ElementLocation"></param>
/// <param name="IsUninitialized"></param>
/// <param name="PropertyReadContext"></param>
internal readonly record struct PropertyReadInfo(
    string PropertyName,
    int StartIndex,
    int EndIndex,
    IMsBuildElementLocation ElementLocation,
    bool IsUninitialized,
    PropertyReadContext PropertyReadContext);
