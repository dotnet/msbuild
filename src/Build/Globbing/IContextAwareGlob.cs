// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Globbing;

/// <summary>
/// Matches globs using path normalization shared within a single matching operation.
/// </summary>
internal interface IContextAwareGlob
{
    bool IsMatch(ref GlobMatchContext context);
}
