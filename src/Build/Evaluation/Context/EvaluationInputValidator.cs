// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// A validation failure with a category and privacy-safe detail, never captured values or diagnostic text.
/// </summary>
internal readonly record struct EvaluationInputValidationFailure(string? Reason, string? Detail);

/// <summary>
/// Checks recorded inputs that can be validated without rerunning external resolvers.
/// </summary>
internal static class EvaluationInputValidator
{
    /// <summary>
    /// Returns true when recording completed without a non-cacheable reason, direct environment reads are unchanged,
    /// and every recorded path still has the required kind and metadata or matching glob results.
    /// </summary>
    /// <param name="inputs">The recorded inputs.</param>
    /// <param name="reason">The first input that differs, or the non-cacheable reason.</param>
    /// <param name="sharedStats">
    /// An optional cache shared across every entry validated in the same build, so the metadata of SDK and package
    /// files is read once per build instead of once per project.
    /// </param>
    /// <param name="measurements">Optional diagnostics sink for where this validation spent its time; never affects the result.</param>
    internal static bool IsFileSystemCurrent(
        EvaluationInputs inputs,
        out string? reason,
        ImmutableFileStatCache? sharedStats = null,
        ValidationMeasurements? measurements = null)
        => IsFileSystemCurrentCore(inputs, captureDetails: false, out reason, out _, sharedStats, measurements);

    /// <summary>
    /// Checks recorded inputs, retaining the legacy reason separately from privacy-safe failure details.
    /// </summary>
    internal static bool IsFileSystemCurrent(
        EvaluationInputs inputs,
        out string? reason,
        out EvaluationInputValidationFailure failure,
        ImmutableFileStatCache? sharedStats = null,
        ValidationMeasurements? measurements = null)
        => IsFileSystemCurrentCore(inputs, captureDetails: true, out reason, out failure, sharedStats, measurements);

    private static bool IsFileSystemCurrentCore(
        EvaluationInputs inputs,
        bool captureDetails,
        out string? reason,
        out EvaluationInputValidationFailure failure,
        ImmutableFileStatCache? sharedStats,
        ValidationMeasurements? measurements)
    {
        failure = default;
        if (!inputs.IsCacheable)
        {
            reason = $"{inputs.NonCacheable}: {inputs.NonCacheableDetail}";
            if (captureDetails)
            {
                failure = new("NonCacheable", inputs.NonCacheable.ToString());
            }

            return false;
        }

        try
        {
            string? changedGlobDirectory = null;
            foreach (KeyValuePair<string, string?> environmentRead in inputs.EnvironmentReads)
            {
                if (!string.Equals(
                        Environment.GetEnvironmentVariable(environmentRead.Key),
                        environmentRead.Value,
                        StringComparison.Ordinal))
                {
                    reason = environmentRead.Key;
                    if (captureDetails)
                    {
                        failure = new("EnvironmentReadChanged", environmentRead.Key);
                    }

                    return false;
                }
            }

            long statStart = measurements is null ? 0 : ValidationMeasurements.Now();
            try
            {
                foreach (KeyValuePair<string, FileDependency> file in inputs.Files)
                {
                    if (measurements is not null)
                    {
                        measurements.CountRecorded(file.Value.Kind);
                        if (sharedStats is null)
                        {
                            measurements.LiveStats++;
                        }
                    }

                    if (!(sharedStats is null
                            ? EvaluationInputRecorder.TryStat(file.Key, out FileDependency current)
                            : sharedStats.TryStat(file.Key, out current, measurements)))
                    {
                        reason = file.Key;
                        if (captureDetails)
                        {
                            failure = new("FileSystemInputUnstatable", file.Key);
                        }

                        return false;
                    }

                    bool metadataChanged = current.LastWriteTimeUtc != file.Value.LastWriteTimeUtc
                        || current.Length != file.Value.Length;
                    if (current.Kind != file.Value.Kind
                        || (file.Value.RequiresMetadata && metadataChanged))
                    {
                        reason = file.Key;
                        if (captureDetails)
                        {
                            failure = new("FileSystemInputChanged", file.Key);
                        }

                        return false;
                    }

                    if (file.Value.RequiresGlobValidation && metadataChanged)
                    {
                        changedGlobDirectory ??= file.Key;
                        if (measurements is not null)
                        {
                            measurements.ChangedGlobDirectories++;
                        }
                    }
                }
            }
            finally
            {
                if (measurements is not null)
                {
                    measurements.StatLoopTicks += ValidationMeasurements.Now() - statStart;
                }
            }

            if (changedGlobDirectory is not null && inputs.Globs.IsDefaultOrEmpty)
            {
                reason = changedGlobDirectory;
                if (captureDetails)
                {
                    failure = new("FileSystemInputChanged", reason);
                }

                return false;
            }

            if (!inputs.Globs.IsDefaultOrEmpty)
            {
                // Validating one entry is a single instant, so its globs can share the directory listings that
                // each would otherwise read again from the same tree. Listings never outlive this validation:
                // a file added after it must be visible to the next one.
                ConcurrentDictionary<string, IReadOnlyList<string>>? listings =
                    inputs.Globs.Length > 1 || measurements is not null ? new(StringComparer.Ordinal) : null;
                if (measurements is not null)
                {
                    measurements.GlobsRecorded += inputs.Globs.Length;
                }

                try
                {
                    foreach (GlobDependency glob in inputs.Globs)
                    {
                        // Cached expansions may precede the directory stats captured by this evaluation.
                        if (changedGlobDirectory is null && !glob.FromCache)
                        {
                            if (measurements is not null)
                            {
                                measurements.GlobsSkipped++;
                            }

                            continue;
                        }

                        long replayStart = measurements is null ? 0 : ValidationMeasurements.Now();
                        bool current = glob.IsCurrent(listings);
                        if (measurements is not null)
                        {
                            measurements.GlobReplayTicks += ValidationMeasurements.Now() - replayStart;
                            measurements.GlobsReplayed++;
                            measurements.CountDriver(glob.Driver);
                            if (!glob.UsesFileSystemEntryCache)
                            {
                                measurements.ReplaysWithoutEntryCache++;
                            }

                            if (changedGlobDirectory is not null)
                            {
                                measurements.ReplaysByDirectoryStamp++;
                            }
                            else
                            {
                                measurements.ReplaysByCachedExpansion++;
                            }

                            if (!current)
                            {
                                measurements.ReplayMismatches++;
                            }
                        }

                        if (!current)
                        {
                            reason = changedGlobDirectory ?? glob.ProjectDirectory;
                            if (captureDetails)
                            {
                                failure = new("FileSystemInputChanged", reason);
                            }

                            return false;
                        }
                    }
                }
                finally
                {
                    if (measurements is not null && listings is not null)
                    {
                        measurements.ListedDirectories += listings.Count;
                        foreach (KeyValuePair<string, IReadOnlyList<string>> listing in listings)
                        {
                            measurements.ListedEntries += listing.Value.Count;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (
            !ExceptionHandling.IsCriticalException(ex)
            && ex is not OperationCanceledException
            && ex is not BuildAbortedException)
        {
            // A failed check is a miss, never a failed build.
            reason = ex.Message;
            if (captureDetails)
            {
                failure = new("MetadataCheckException", ex.GetType().Name);
            }

            return false;
        }

        reason = null;
        return true;
    }
}
