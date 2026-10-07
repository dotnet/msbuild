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
    /// <param name="directoryListings">
    /// Optional directory listings kept from earlier replays, so a glob replay reads only the directories that changed.
    /// </param>
    internal static bool IsFileSystemCurrent(
        EvaluationInputs inputs,
        out string? reason,
        ImmutableFileStatCache? sharedStats = null,
        ValidatedDirectoryListings? directoryListings = null)
        => IsFileSystemCurrentCore(inputs, captureDetails: false, out reason, out _, sharedStats, directoryListings);

    /// <summary>
    /// Checks recorded inputs, retaining the legacy reason separately from privacy-safe failure details.
    /// </summary>
    internal static bool IsFileSystemCurrent(
        EvaluationInputs inputs,
        out string? reason,
        out EvaluationInputValidationFailure failure,
        ImmutableFileStatCache? sharedStats = null,
        ValidatedDirectoryListings? directoryListings = null)
        => IsFileSystemCurrentCore(inputs, captureDetails: true, out reason, out failure, sharedStats, directoryListings);

    private static bool IsFileSystemCurrentCore(
        EvaluationInputs inputs,
        bool captureDetails,
        out string? reason,
        out EvaluationInputValidationFailure failure,
        ImmutableFileStatCache? sharedStats,
        ValidatedDirectoryListings? directoryListings)
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

            List<KeyValuePair<string, FileDependency>>? changedGlobDirectories = null;
            List<KeyValuePair<string, FileDependency>>? directoryStates = null;
            foreach (KeyValuePair<string, FileDependency> file in inputs.Files)
            {
                if (!(sharedStats is null
                        ? EvaluationInputRecorder.TryStat(file.Key, out FileDependency current)
                        : sharedStats.TryStat(file.Key, out current)))
                {
                    reason = file.Key;
                    if (captureDetails)
                    {
                        failure = new("FileSystemInputUnstatable", file.Key);
                    }

                    return false;
                }

                // A directory whose changes a matching replay already vouched for is compared with that state, so the
                // same unrelated change does not trigger the same replay on every later validation.
                FileDependency recorded = file.Value;
                if (recorded.RequiresGlobValidation
                    && !recorded.RequiresMetadata
                    && inputs.TryGetValidatedGlobDirectory(file.Key, out FileDependency validated))
                {
                    recorded = validated;
                }

                bool metadataChanged = current.LastWriteTimeUtc != recorded.LastWriteTimeUtc
                    || current.Length != recorded.Length;
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

                if (file.Value.RequiresGlobValidation)
                {
                    if (directoryListings is not null)
                    {
                        (directoryStates ??= []).Add(new(file.Key, current));
                    }

                    if (metadataChanged)
                    {
                        changedGlobDirectory ??= file.Key;
                        (changedGlobDirectories ??= []).Add(new(file.Key, current));
                    }
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
                bool replayNeeded = changedGlobDirectory is not null;
                if (!replayNeeded)
                {
                    foreach (GlobDependency glob in inputs.Globs)
                    {
                        if (glob.FromCache)
                        {
                            replayNeeded = true;
                            break;
                        }
                    }
                }

                // Validating one entry is a single instant, so its globs can share the directory listings that
                // each would otherwise read again from the same tree. A listing taken from an earlier replay is only
                // used while its directory still has the state it was read at, so a file added since is always visible.
                ValidatedDirectoryListings.Replay? replay = null;
                ConcurrentDictionary<string, IReadOnlyList<string>>? listings = null;
                if (replayNeeded)
                {
                    if (directoryListings is not null && directoryStates is not null)
                    {
                        replay = directoryListings.BeginReplay(directoryStates);
                        listings = replay.Listings;
                    }
                    else if (inputs.Globs.Length > 1)
                    {
                        listings = new(StringComparer.Ordinal);
                    }
                }

                foreach (GlobDependency glob in inputs.Globs)
                {
                    // Cached expansions may precede the directory stats captured by this evaluation.
                    if ((changedGlobDirectory is not null || glob.FromCache) && !glob.IsCurrent(listings))
                    {
                        reason = changedGlobDirectory ?? glob.ProjectDirectory;
                        if (captureDetails)
                        {
                            failure = new("FileSystemInputChanged", reason);
                        }

                        return false;
                    }
                }

                replay?.KeepReadListings(DateTime.UtcNow);
                if (changedGlobDirectories is not null)
                {
                    RememberQuietDirectories(inputs, changedGlobDirectories);
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

    /// <summary>
    /// After every glob of an entry matched its recorded result, remembers the state each changed directory had when it
    /// was observed, which preceded the replay. A change after that observation gives the directory a newer timestamp,
    /// and a change a coarse timestamp could hide is excluded by only trusting timestamps older than the clock's reach.
    /// </summary>
    private static void RememberQuietDirectories(
        EvaluationInputs inputs,
        List<KeyValuePair<string, FileDependency>> changedDirectories)
    {
        DateTime newestTrusted = DateTime.UtcNow - ValidatedDirectoryListings.RacyTimestampWindow;
        List<KeyValuePair<string, FileDependency>>? quiet = null;
        foreach (KeyValuePair<string, FileDependency> directory in changedDirectories)
        {
            if (directory.Value.LastWriteTimeUtc <= newestTrusted)
            {
                (quiet ??= []).Add(directory);
            }
        }

        if (quiet is not null)
        {
            inputs.RememberValidatedGlobDirectories(quiet);
        }
    }
}
