// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.TelemetryInfra;

internal static class EvaluationMetrics
{
    internal const string MeterName = "Microsoft.Build";
    internal const string ProjectEvaluationCountName = "msbuild.project.evaluations";
    internal const string ProjectEvaluationDurationName = "msbuild.project.evaluation.duration";

    internal const string StageTagName = "msbuild.project.evaluation.stage";
    internal const string OriginTagName = "msbuild.project.evaluation.origin";
    internal const string SucceededTagName = "msbuild.project.evaluation.succeeded";

    internal const string FullStage = "full";
    internal const string BuildSubmissionOrigin = "build_submission";
    internal const string StandaloneOrigin = "standalone";

    private static readonly object s_boxedTrue = true;
    private static readonly object s_boxedFalse = false;
    private static int s_disabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long GetEvaluationStartTimestamp()
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return Instruments.ProjectEvaluationDuration.Enabled ? Stopwatch.GetTimestamp() : 0;
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
            return 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RecordProjectEvaluation(
        long startTimestamp,
        bool isBuildSubmission,
        bool succeeded)
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            bool countEnabled = Instruments.ProjectEvaluationCount.Enabled;
            bool durationEnabled = startTimestamp != 0 && Instruments.ProjectEvaluationDuration.Enabled;
            if (!countEnabled && !durationEnabled)
            {
                return;
            }

            TagList tags = default;
            tags.Add(StageTagName, FullStage);
            tags.Add(OriginTagName, isBuildSubmission ? BuildSubmissionOrigin : StandaloneOrigin);
            tags.Add(SucceededTagName, succeeded ? s_boxedTrue : s_boxedFalse);

            if (countEnabled)
            {
                Instruments.ProjectEvaluationCount.Add(1, in tags);
            }

            if (durationEnabled)
            {
                double elapsedSeconds = (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
                Instruments.ProjectEvaluationDuration.Record(elapsedSeconds, in tags);
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    internal static void ResetForTests()
    {
        Volatile.Write(ref s_disabled, 0);
    }

    private static void Disable(Exception ex)
    {
        Volatile.Write(ref s_disabled, 1);
        Debug.WriteLine($"MSBuild evaluation metrics disabled after an instrumentation failure: {ex}");
    }

    private static class Instruments
    {
        private static readonly Meter s_meter = new(MeterName);

        internal static readonly Counter<long> ProjectEvaluationCount = s_meter.CreateCounter<long>(
            ProjectEvaluationCountName,
            unit: "{evaluation}",
            description: "Number of MSBuild project evaluations.");

        internal static readonly Histogram<double> ProjectEvaluationDuration = s_meter.CreateHistogram<double>(
            ProjectEvaluationDurationName,
            unit: "s",
            description: "Duration of MSBuild project evaluations.",
            tags: null,
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries =
                [
                    0.001,
                    0.005,
                    0.01,
                    0.025,
                    0.05,
                    0.1,
                    0.25,
                    0.5,
                    1,
                    2.5,
                    5,
                    10,
                    30,
                ],
            });
    }
}
