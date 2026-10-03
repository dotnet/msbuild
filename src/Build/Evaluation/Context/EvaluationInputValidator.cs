// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Checks recorded file-system inputs without authorizing reuse of an evaluation.
/// </summary>
internal static class EvaluationInputValidator
{
    /// <summary>
    /// Returns true when recording completed without a non-cacheable reason and every recorded path still has the
    /// same kind, last-write time, and length.
    /// </summary>
    /// <param name="inputs">The recorded inputs.</param>
    /// <param name="reason">The first input that differs, or the non-cacheable reason.</param>
    internal static bool IsFileSystemCurrent(EvaluationInputs inputs, out string? reason)
    {
        if (!inputs.IsCacheable)
        {
            reason = $"{inputs.NonCacheable}: {inputs.NonCacheableDetail}";
            return false;
        }

        try
        {
            foreach (KeyValuePair<string, FileDependency> file in inputs.Files)
            {
                if (!EvaluationInputRecorder.TryStat(file.Key, out FileDependency current) || current != file.Value)
                {
                    reason = file.Key;
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
            return false;
        }

        reason = null;
        return true;
    }
}
