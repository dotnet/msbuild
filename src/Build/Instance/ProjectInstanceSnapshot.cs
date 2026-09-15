// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Execution;

/// <summary>
/// An immutable template for creating mutable copies of an evaluated <see cref="ProjectInstance"/>.
/// </summary>
/// <remarks>
/// Mutable project state is copied for each materialization. Immutable
/// <see cref="ProjectTargetInstance"/> objects are shared with the source project.
/// </remarks>
internal sealed class ProjectInstanceSnapshot
{
    private readonly ProjectInstance _template;
    private readonly long _estimatedRetainedSizeBytes;
    private readonly string _toolsVersion;

    private ProjectInstanceSnapshot(
        ProjectInstance template,
        long estimatedRetainedSizeBytes,
        string toolsVersion)
    {
        _template = template;
        _estimatedRetainedSizeBytes = estimatedRetainedSizeBytes;
        _toolsVersion = toolsVersion;
    }

    /// <summary>
    /// Gets an approximate size used to bound the snapshot cache.
    /// </summary>
    /// <remarks>
    /// On .NET this is the number of bytes allocated while creating the template. On .NET Framework
    /// it is a count-based heuristic. It is not an exact managed-object retained-size calculation.
    /// </remarks>
    internal long EstimatedRetainedSizeBytes => _estimatedRetainedSizeBytes;

    /// <summary>
    /// Creates a snapshot from a project instance that is not being mutated concurrently.
    /// </summary>
    internal static ProjectInstanceSnapshot Create(ProjectInstance project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.EvaluationStage != ProjectEvaluationStage.Full)
        {
            throw new InvalidOperationException(
                "Project instance snapshots require a full evaluation.");
        }

        if (project.IsImmutable)
        {
            throw new InvalidOperationException(
                "Project instance snapshots require a mutable source.");
        }

        string toolsVersion = project.ToolsVersion;
#if NET
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
#endif

        ProjectInstance template = project.DeepCopyAllState(isImmutable: true);
        template.PrepareForSnapshotTemplate();

#if NET
        long estimatedRetainedSizeBytes =
            Math.Max(1, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
#else
        long estimatedRetainedSizeBytes = Math.Max(
            1,
            (long)project.Properties.Count * 96 +
            (long)project.Items.Count * 160 +
            (long)project.Targets.Count * 512 +
            (long)project.ItemDefinitions.Count * 160);
#endif

        return new ProjectInstanceSnapshot(
            template,
            estimatedRetainedSizeBytes,
            toolsVersion);
    }

    /// <summary>
    /// Materializes a new mutable project instance from the immutable template.
    /// </summary>
    internal ProjectInstance Materialize(
        BuildParameters buildParameters,
        int evaluationId,
        ILoggingService? loggingService = null,
        BuildEventContext? buildEventContext = null)
    {
        ProjectInstance materialized = _template.DeepCopyAllState(isImmutable: false);
        if (!materialized.TryReinitializeSnapshotMaterialization(
            buildParameters,
            evaluationId,
            _toolsVersion,
            loggingService,
            buildEventContext))
        {
            throw new InvalidOperationException(
                $"Could not resolve toolset '{_toolsVersion}' for snapshot materialization.");
        }

        if (materialized.IsImmutable)
        {
            throw new InvalidOperationException(
                "Snapshot materialization must produce a mutable project instance.");
        }

        return materialized;
    }
}
