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
    /// <summary>The process-wide MSBuild meter.</summary>
    internal const string MeterName = "Microsoft.Build";

    /// <summary>Counter incremented once per evaluation.</summary>
    internal const string ProjectEvaluationCountName = "msbuild.project.evaluations";

    /// <summary>Elapsed seconds for one evaluation.</summary>
    internal const string ProjectEvaluationDurationName = "msbuild.project.evaluation.duration";

    /// <summary>Elapsed seconds for one evaluation pass.</summary>
    internal const string ProjectEvaluationPassDurationName = "msbuild.project.evaluation.pass.duration";

    /// <summary>The requested evaluation stopping stage.</summary>
    internal const string StageTagName = "msbuild.project.evaluation.stage";

    /// <summary>The pass measured by the pass-duration instrument.</summary>
    internal const string PassTagName = "msbuild.project.evaluation.pass";

    /// <summary>Distinguishes requested-build evaluation from hidden or preflight evaluation.</summary>
    internal const string OriginTagName = "msbuild.project.evaluation.origin";

    /// <summary>Whether evaluation completed successfully.</summary>
    internal const string SucceededTagName = "msbuild.project.evaluation.succeeded";

    /// <summary>Marks evaluation performed for an active build request.</summary>
    internal const string BuildSubmissionOrigin = "build_submission";

    /// <summary>Marks object-model, graph, reevaluation, or discovery work outside a build request.</summary>
    internal const string OutsideBuildSubmissionOrigin = "outside_build_submission";

    /// <summary>Disables Metrics after an instrumentation failure so evaluation can continue safely.</summary>
    private static int s_disabled;

    internal static long EvaluateStart()
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return EvaluateStartCore();
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
            return 0;
        }
    }

    // Keep Metrics type resolution inside EvaluateStart's catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long EvaluateStartCore() =>
        Instruments.ProjectEvaluationDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    internal static void EvaluateStop(
        long startTimestamp,
        ProjectEvaluationStage stage,
        int submissionId,
        bool succeeded)
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            EvaluateStopCore(startTimestamp, stage, submissionId, succeeded);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    // Keep Metrics type resolution inside EvaluateStop's catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EvaluateStopCore(
        long startTimestamp,
        ProjectEvaluationStage stage,
        int submissionId,
        bool succeeded)
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
        tags.Add(
            OriginTagName,
            submissionId != BuildEventContext.InvalidSubmissionId ? BuildSubmissionOrigin : OutsideBuildSubmissionOrigin);
        tags.Add(SucceededTagName, succeeded);

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

    internal static void EvaluatePass0Stop(long startTimestamp, ProjectEvaluationStage stage, int submissionId) =>
        EvaluatePassStop(startTimestamp, EvaluationPass.InitialProperties, stage, submissionId);

    internal static void EvaluatePass1Stop(long startTimestamp, ProjectEvaluationStage stage, int submissionId) =>
        EvaluatePassStop(startTimestamp, EvaluationPass.Properties, stage, submissionId);

    internal static void EvaluatePass2Stop(long startTimestamp, ProjectEvaluationStage stage, int submissionId) =>
        EvaluatePassStop(startTimestamp, EvaluationPass.ItemDefinitionGroups, stage, submissionId);

    internal static void EvaluatePass3Stop(
        long itemsStartTimestamp,
        long itemsEndTimestamp,
        long lazyItemsStartTimestamp,
        long lazyItemsEndTimestamp,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        EvaluatePassStop(itemsStartTimestamp, itemsEndTimestamp, EvaluationPass.Items, stage, submissionId);
        EvaluatePassStop(lazyItemsStartTimestamp, lazyItemsEndTimestamp, EvaluationPass.LazyItems, stage, submissionId);
    }

    internal static void EvaluatePass4Stop(long startTimestamp, ProjectEvaluationStage stage, int submissionId) =>
        EvaluatePassStop(startTimestamp, EvaluationPass.UsingTasks, stage, submissionId);

    internal static void EvaluatePass5Stop(long startTimestamp, ProjectEvaluationStage stage, int submissionId) =>
        EvaluatePassStop(startTimestamp, EvaluationPass.Targets, stage, submissionId);

    internal static long EvaluatePassStart()
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return EvaluatePassStartCore();
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
            return 0;
        }
    }

    // Keep Metrics type resolution inside EvaluatePassStart's catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long EvaluatePassStartCore() =>
        Instruments.ProjectEvaluationPassDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long EvaluatePassEnd(long startTimestamp)
    {
        if (startTimestamp == 0 || Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return Stopwatch.GetTimestamp();
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
        EvaluationPass.LazyItems => "lazy_items",
        EvaluationPass.UsingTasks => "using_tasks",
        EvaluationPass.Targets => "targets",
        _ => "unknown",
    };

    private static void EvaluatePassStop(
        long startTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        if (startTimestamp == 0 || Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            long endTimestamp = Stopwatch.GetTimestamp();
            EvaluatePassStopCore(startTimestamp, endTimestamp, pass, stage, submissionId);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    private static void EvaluatePassStop(
        long startTimestamp,
        long endTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        if (startTimestamp == 0 || endTimestamp == 0 || Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            EvaluatePassStopCore(startTimestamp, endTimestamp, pass, stage, submissionId);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable(ex);
        }
    }

    // Keep Metrics type resolution inside EvaluatePassStop's catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EvaluatePassStopCore(
        long startTimestamp,
        long endTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        if (!Instruments.ProjectEvaluationPassDuration.Enabled)
        {
            return;
        }

        RecordPassDuration(startTimestamp, endTimestamp, pass, stage, submissionId);
    }

    private static void RecordPassDuration(
        long startTimestamp,
        long endTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        TagList tags = default;
        tags.Add(StageTagName, GetStageName(stage));
        tags.Add(PassTagName, GetPassName(pass));
        tags.Add(
            OriginTagName,
            submissionId != BuildEventContext.InvalidSubmissionId ? BuildSubmissionOrigin : OutsideBuildSubmissionOrigin);

        double elapsedSeconds = (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
        Instruments.ProjectEvaluationPassDuration.Record(elapsedSeconds, in tags);
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
            description: "Duration of MSBuild project evaluations.");

        internal static readonly Histogram<double> ProjectEvaluationPassDuration = s_meter.CreateHistogram<double>(
            ProjectEvaluationPassDurationName,
            unit: "s",
            description: "Duration of MSBuild project evaluation passes.");
    }
}
