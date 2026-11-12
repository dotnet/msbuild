// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Utilities;

/// <summary>
/// What should happen if multiple versions of a given productfamily or sdk name are found
/// </summary>
public enum MultipleVersionSupport
{
    /// <summary>
    /// No action should be taken if multiple versions are detected
    /// </summary>
    Allow = 0,

    /// <summary>
    /// Log  warning
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Log an error
    /// </summary>
    Error = 2,
}
