// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Bag of information for a performed property write.
/// </summary>
/// <param name="PropertyName">Name of the property.</param>
/// <param name="IsEmpty">Was any value written? (E.g. if we set propA with value propB, while propB is undefined - the isEmpty will be true)</param>
/// <param name="ElementLocation">Location of the property write</param>
internal readonly record struct PropertyWriteInfo(
    string PropertyName,
    bool IsEmpty,
    IMsBuildElementLocation? ElementLocation);
