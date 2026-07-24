// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.TelemetryInfra;

internal static class EvaluationMetrics
{
    internal const string MeterName = "Microsoft.Build";
    internal const string ProjectEvaluationCountName = "msbuild.project.evaluations";
    internal const string ProjectEvaluationDurationName = "msbuild.project.evaluation.duration";
    internal const string ProjectEvaluationPassDurationName = "msbuild.project.evaluation.pass.duration";

    internal const string StageTagName = "msbuild.project.evaluation.stage";
    internal const string PassTagName = "msbuild.project.evaluation.pass";
    internal const string OriginTagName = "msbuild.project.evaluation.origin";
    internal const string SucceededTagName = "msbuild.project.evaluation.succeeded";

    internal const string BuildSubmissionOrigin = "build_submission";
    internal const string StandaloneOrigin = "standalone";

    private static readonly object s_boxedTrue = true;
    private static readonly object s_boxedFalse = false;
    private static int s_disabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long EvaluateStart()
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
    internal static void EvaluateStop(
        long startTimestamp,
        ProjectEvaluationStage stage,
        bool isBuildSubmission,
        bool succeeded)
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            long endTimestamp = startTimestamp != 0 ? Stopwatch.GetTimestamp() : 0;
            bool countEnabled = Instruments.ProjectEvaluationCount.Enabled;
            bool durationEnabled = startTimestamp != 0 && Instruments.ProjectEvaluationDuration.Enabled;
            if (!countEnabled && !durationEnabled)
            {
                return;
            }

            TagList tags = default;
            tags.Add(StageTagName, GetStageName(stage));
            tags.Add(OriginTagName, isBuildSubmission ? BuildSubmissionOrigin : StandaloneOrigin);
            tags.Add(SucceededTagName, succeeded ? s_boxedTrue : s_boxedFalse);

            if (countEnabled)
            {
                Instruments.ProjectEvaluationCount.Add(1, in tags);
            }

            if (durationEnabled)
            {
                double elapsedSeconds = (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
                Instruments.ProjectEvaluationDuration.Record(elapsedSeconds, in tags);
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long EvaluatePassStart()
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return Instruments.ProjectEvaluationPassDuration.Enabled ? Stopwatch.GetTimestamp() : 0;
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
            return 0;
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

    private static string GetStageName(ProjectEvaluationStage stage) => stage switch
    {
        ProjectEvaluationStage.Properties => "properties",
        ProjectEvaluationStage.ItemDefinitions => "item_definitions",
        ProjectEvaluationStage.Items => "items",
        ProjectEvaluationStage.UsingTasks => "using_tasks",
        ProjectEvaluationStage.Full => "full",
        _ => "unknown",
    };

    private static string GetPassName(EvaluationPass pass) => pass switch
    {
        EvaluationPass.InitialProperties => "initial_properties",
        EvaluationPass.Properties => "properties",
        EvaluationPass.ItemDefinitionGroups => "item_definitions",
        EvaluationPass.Items => "items",
        EvaluationPass.UsingTasks => "using_tasks",
        EvaluationPass.Targets => "targets",
        _ => "unknown",
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EvaluatePassStop(
        long startTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        bool isBuildSubmission)
    {
        if (startTimestamp == 0 || Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            long endTimestamp = Stopwatch.GetTimestamp();
            if (!Instruments.ProjectEvaluationPassDuration.Enabled)
            {
                return;
            }

            TagList tags = default;
            tags.Add(StageTagName, GetStageName(stage));
            tags.Add(PassTagName, GetPassName(pass));
            tags.Add(OriginTagName, isBuildSubmission ? BuildSubmissionOrigin : StandaloneOrigin);

            double elapsedSeconds = (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
            Instruments.ProjectEvaluationPassDuration.Record(elapsedSeconds, in tags);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    private static class Instruments
    {
        private static readonly Meter s_meter = new(MeterName);
        private static readonly InstrumentAdvice<double> s_durationAdvice = new()
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
        };

        internal static readonly Counter<long> ProjectEvaluationCount = s_meter.CreateCounter<long>(
            ProjectEvaluationCountName,
            unit: "{evaluation}",
            description: "Number of MSBuild project evaluations.");

        internal static readonly Histogram<double> ProjectEvaluationDuration = s_meter.CreateHistogram<double>(
            ProjectEvaluationDurationName,
            unit: "s",
            description: "Duration of MSBuild project evaluations.",
            tags: null,
            advice: s_durationAdvice);

        internal static readonly Histogram<double> ProjectEvaluationPassDuration = s_meter.CreateHistogram<double>(
            ProjectEvaluationPassDurationName,
            unit: "s",
            description: "Duration of MSBuild project evaluation passes.",
            tags: null,
            advice: s_durationAdvice);
    }
}
