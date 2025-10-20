// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// The severity of reported result (or preconfigured or user configured severity for a rule).
/// </summary>
public enum BuildAnalyzerResultSeverity
{
    Info,
    Warning,
    Error,
}
