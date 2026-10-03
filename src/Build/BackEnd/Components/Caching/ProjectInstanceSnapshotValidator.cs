// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// The result of validating a project instance snapshot for reuse.
/// </summary>
internal enum ProjectInstanceSnapshotValidationResult
{
    /// <summary>
    /// The snapshot must not be reused.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// The snapshot may be reused.
    /// </summary>
    Valid,
}

/// <summary>
/// Validates whether a cached project instance snapshot may be reused.
/// </summary>
internal interface IProjectInstanceSnapshotValidator
{
    /// <summary>
    /// Determines whether the snapshot may be reused for the specified evaluation request.
    /// </summary>
    /// <remarks>
    /// Implementations must fail closed and return <see cref="ProjectInstanceSnapshotValidationResult.Invalid"/>
    /// when project or import contents, file enumeration, relevant environment values, SDK and toolset
    /// resolution state, or any other evaluation input not represented by the key cannot be verified.
    /// </remarks>
    ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry);
}

/// <summary>
/// The production default validator, which rejects every snapshot until invalidation is implemented.
/// </summary>
internal sealed class RejectingProjectInstanceSnapshotValidator : IProjectInstanceSnapshotValidator
{
    internal static RejectingProjectInstanceSnapshotValidator Instance { get; } = new();

    private RejectingProjectInstanceSnapshotValidator()
    {
    }

    public ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        return ProjectInstanceSnapshotValidationResult.Invalid;
    }
}
