// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Evaluation.Context;
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

internal sealed class EvaluationInputsSnapshotValidationData : IProjectInstanceSnapshotValidationData
{
    internal EvaluationInputsSnapshotValidationData(EvaluationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Inputs = inputs;
    }

    internal EvaluationInputs Inputs { get; }

    public long RetainedSizeBytes
    {
        get
        {
            long size = 192;
            foreach (KeyValuePair<string, FileDependency> file in Inputs.Files)
            {
                size = RetainedSizeEstimator.AddString(
                    RetainedSizeEstimator.Add(size, 80),
                    file.Key);
            }

            foreach (KeyValuePair<string, string?> environmentRead in Inputs.EnvironmentReads)
            {
                size = RetainedSizeEstimator.AddString(
                    RetainedSizeEstimator.Add(size, 64),
                    environmentRead.Key);
                size = RetainedSizeEstimator.AddString(size, environmentRead.Value);
            }

            foreach (SdkDependency sdk in Inputs.SdkResolutions)
            {
                size = RetainedSizeEstimator.Add(size, 128);
                size = RetainedSizeEstimator.AddString(size, sdk.Reference.Name);
                size = RetainedSizeEstimator.AddString(size, sdk.Reference.Version);
                size = RetainedSizeEstimator.AddString(size, sdk.Reference.MinimumVersion);
                size = RetainedSizeEstimator.AddString(size, sdk.Context.ReferenceLocation.File);
                size = RetainedSizeEstimator.AddString(size, sdk.Context.SolutionPath);
                size = RetainedSizeEstimator.AddString(size, sdk.Context.ProjectPath);
                size = RetainedSizeEstimator.Add(size, sdk.Result.RetainedSizeBytes);
            }

            foreach (RegistryRead registryRead in Inputs.RegistryReads)
            {
                size = RetainedSizeEstimator.Add(size, 96);
                size = RetainedSizeEstimator.AddString(size, registryRead.KeyName);
                size = RetainedSizeEstimator.AddString(size, registryRead.ValueName);
                size = RetainedSizeEstimator.AddStrings(size, registryRead.RequestedViews);
                size = AddRegistryValueSize(size, registryRead.Value);
            }

            size = RetainedSizeEstimator.AddString(size, Inputs.NonCacheableDetail);
            return Math.Max(1, size);
        }
    }

    private static long AddRegistryValueSize(long size, object? value) =>
        value switch
        {
            string text => RetainedSizeEstimator.AddString(size, text),
            ImmutableArray<byte> bytes => RetainedSizeEstimator.Add(size, bytes.Length),
            ImmutableArray<char> characters => RetainedSizeEstimator.Add(size, characters.Length * 2L),
            ImmutableArray<string> strings => RetainedSizeEstimator.AddStrings(size, strings),
            null => size,
            _ => RetainedSizeEstimator.Add(size, 32),
        };
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
        RetainedSizeBytes = RetainedSizeEstimator.Add(
            snapshot.EstimatedRetainedSizeBytes,
            validationData.RetainedSizeBytes);
    }

    internal ProjectInstanceSnapshot Snapshot { get; }

    internal IProjectInstanceSnapshotValidationData ValidationData { get; }

    internal long RetainedSizeBytes { get; }
}
