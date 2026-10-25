// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Specifies how item metadata values are compared when using <c>MatchOnMetadata</c>.
/// </summary>
public enum MatchOnMetadataOptions
{
    /// <summary>
    ///  Compares metadata values using ordinal, case-sensitive string comparison.
    /// </summary>
    CaseSensitive,

    /// <summary>
    ///  Compares metadata values using ordinal, case-insensitive string comparison.
    /// </summary>
    CaseInsensitive,

    /// <summary>
    ///  Normalizes metadata values as paths and compares them using the platform's path-comparison rules.
    /// </summary>
    PathLike,

    /// <summary>
    ///  Uses the default metadata comparison mode, equivalent to <see cref="CaseSensitive"/>.
    /// </summary>
    Default = CaseSensitive,
}
