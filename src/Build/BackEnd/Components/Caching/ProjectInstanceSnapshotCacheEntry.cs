// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Execution;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Immutable validation data retained with a project instance snapshot. Implementations must capture
/// the evaluation inputs not represented by <see cref="ProjectInstanceSnapshotCacheKey"/> that their
/// paired <see cref="IProjectInstanceSnapshotValidator"/> requires.
/// </summary>
internal interface IProjectInstanceSnapshotValidationData
{
    /// <summary>
    /// Gets the number of bytes retained by this validation data.
    /// </summary>
    long RetainedSizeBytes { get; }
}

/// <summary>
/// Placeholder validation data that carries no validation inputs.
/// </summary>
internal sealed class EmptyProjectInstanceSnapshotValidationData : IProjectInstanceSnapshotValidationData
{
    internal static EmptyProjectInstanceSnapshotValidationData Instance { get; } = new();

    private EmptyProjectInstanceSnapshotValidationData()
    {
    }

    public long RetainedSizeBytes => 0;
}

/// <summary>
/// A project instance snapshot and the data required to validate its reuse.
/// </summary>
internal sealed class ProjectInstanceSnapshotCacheEntry
{
    internal ProjectInstanceSnapshotCacheEntry(
        ProjectInstanceSnapshot snapshot,
        IProjectInstanceSnapshotValidationData validationData)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(validationData);
        if (validationData.RetainedSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(validationData));
        }

        Snapshot = snapshot;
        ValidationData = validationData;
        RetainedSizeBytes =
            checked(snapshot.EstimatedRetainedSizeBytes + validationData.RetainedSizeBytes);
    }

    internal ProjectInstanceSnapshot Snapshot { get; }

    internal IProjectInstanceSnapshotValidationData ValidationData { get; }

    internal long RetainedSizeBytes { get; }
}
