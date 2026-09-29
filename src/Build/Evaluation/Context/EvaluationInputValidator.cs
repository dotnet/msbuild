// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    /// and every recorded path still has the same kind, timestamp, and length.
    /// </summary>
    /// <param name="inputs">The recorded inputs.</param>
    /// <param name="reason">The first input that differs, or the non-cacheable reason.</param>
    internal static bool IsFileSystemCurrent(EvaluationInputs inputs, out string? reason)
        => IsFileSystemCurrentCore(inputs, captureDetails: false, out reason, out _);

    /// <summary>
    /// Checks recorded inputs, retaining the legacy reason separately from privacy-safe failure details.
    /// </summary>
    internal static bool IsFileSystemCurrent(
        EvaluationInputs inputs,
        out string? reason,
        out EvaluationInputValidationFailure failure)
        => IsFileSystemCurrentCore(inputs, captureDetails: true, out reason, out failure);

    private static bool IsFileSystemCurrentCore(
        EvaluationInputs inputs,
        bool captureDetails,
        out string? reason,
        out EvaluationInputValidationFailure failure)
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

            foreach (KeyValuePair<string, FileDependency> file in inputs.Files)
            {
                if (!EvaluationInputRecorder.TryStat(file.Key, out FileDependency current))
                {
                    reason = file.Key;
                    if (captureDetails)
                    {
                        failure = new("FileSystemInputUnstatable", file.Key);
                    }

                    return false;
                }

                if (current != file.Value)
                {
                    reason = file.Key;
                    if (captureDetails)
                    {
                        failure = new("FileSystemInputChanged", file.Key);
                    }

                    return false;
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
