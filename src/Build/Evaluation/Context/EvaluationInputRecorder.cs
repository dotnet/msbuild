// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Collects the inputs one evaluation consumes. Created only when <see cref="Traits.RecordEvaluationInputs"/> is set,
/// so the rest of the engine pays one null check per seam when recording is off.
/// Once the evaluation is known to be non-cacheable, further observations are skipped: the manifest will not be used.
/// </summary>
internal sealed class EvaluationInputRecorder
{
    private NonCacheableReason _nonCacheable;
    private string? _nonCacheableDetail;
    private bool _frozen;

    /// <summary>
    /// False once the evaluation is non-cacheable, since the manifest will not be used, or once <see cref="Freeze"/>
    /// handed the collections out.
    /// </summary>
    private bool IsRecording => !_frozen && _nonCacheable == NonCacheableReason.None;

    internal static EvaluationInputRecorder? CreateIfEnabled() =>
        Traits.Instance.RecordEvaluationInputs ? new EvaluationInputRecorder() : null;

    internal void MarkNonCacheable(NonCacheableReason reason, string? detail = null)
    {
        if (IsRecording)
        {
            _nonCacheable = reason;
            _nonCacheableDetail = detail;
        }
    }

    /// <summary>
    /// Ends recording and hands the collected inputs over without copying them.
    /// </summary>
    internal EvaluationInputs Freeze(EvaluationInputKey key)
    {
        _frozen = true;
        return new EvaluationInputs(
            key,
            _nonCacheable,
            _nonCacheableDetail);
    }
}
