// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.TelemetryInfra;

internal static class EvaluationInstrumentation
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

    internal static EvaluationScope StartEvaluation(
        string projectFile,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        MSBuildEventSource.Log.EvaluateStart(projectFile);
        return new EvaluationScope(projectFile, stage, submissionId, StartEvaluationMetrics());
    }

    internal static EvaluationPassScope StartPass(
        string projectFile,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        string projectFileForStop = string.IsNullOrEmpty(projectFile) ? "(null)" : projectFile;
        string projectFileForStart =
            pass == EvaluationPass.InitialProperties
                ? projectFile
                : projectFileForStop;
        WritePassStart(pass, projectFileForStart);
        return new EvaluationPassScope(projectFileForStop, pass, stage, submissionId, StartPassMetrics());
    }

    internal static void ResetForTests()
    {
        Volatile.Write(ref s_metricsDisabled, 0);
    }

    private static void CompletePass(
        string projectFile,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId,
        long startTimestamp)
    {
        long endTimestamp = GetMetricsEndTimestamp(startTimestamp);
        WritePassStop(pass, projectFile);
        RecordPassMetrics(startTimestamp, endTimestamp, pass, stage, submissionId);
    }

    private static void WritePassStart(EvaluationPass pass, string projectFile)
    {
        switch (pass)
        {
            case EvaluationPass.InitialProperties:
                MSBuildEventSource.Log.EvaluatePass0Start(projectFile);
                break;
            case EvaluationPass.Properties:
                MSBuildEventSource.Log.EvaluatePass1Start(projectFile);
                break;
            case EvaluationPass.ItemDefinitionGroups:
                MSBuildEventSource.Log.EvaluatePass2Start(projectFile);
                break;
            case EvaluationPass.Items:
                MSBuildEventSource.Log.EvaluatePass3Start(projectFile);
                break;
            case EvaluationPass.UsingTasks:
                MSBuildEventSource.Log.EvaluatePass4Start(projectFile);
                break;
            case EvaluationPass.Targets:
                MSBuildEventSource.Log.EvaluatePass5Start(projectFile);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pass), pass, "Unsupported instrumented evaluation pass.");
        }
    }

    private static void WritePassStop(EvaluationPass pass, string projectFile)
    {
        switch (pass)
        {
            case EvaluationPass.InitialProperties:
                MSBuildEventSource.Log.EvaluatePass0Stop(projectFile);
                break;
            case EvaluationPass.Properties:
                MSBuildEventSource.Log.EvaluatePass1Stop(projectFile);
                break;
            case EvaluationPass.ItemDefinitionGroups:
                MSBuildEventSource.Log.EvaluatePass2Stop(projectFile);
                break;
            case EvaluationPass.Items:
                MSBuildEventSource.Log.EvaluatePass3Stop(projectFile);
                break;
            case EvaluationPass.UsingTasks:
                MSBuildEventSource.Log.EvaluatePass4Stop(projectFile);
                break;
            case EvaluationPass.Targets:
                MSBuildEventSource.Log.EvaluatePass5Stop(projectFile);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pass), pass, "Unsupported instrumented evaluation pass.");
        }
    }

    internal readonly struct EvaluationScope : IDisposable
    {
        private readonly string _projectFile;
        private readonly ProjectEvaluationStage _stage;
        private readonly int _submissionId;
        private readonly long _metricsStartTimestamp;

        internal EvaluationScope(
            string projectFile,
            ProjectEvaluationStage stage,
            int submissionId,
            long metricsStartTimestamp)
        {
            _projectFile = projectFile;
            _stage = stage;
            _submissionId = submissionId;
            _metricsStartTimestamp = metricsStartTimestamp;
        }

        internal void CompleteEvaluation(bool succeeded)
        {
            long endTimestamp = GetMetricsEndTimestamp(_metricsStartTimestamp);
            RecordEvaluationMetrics(_metricsStartTimestamp, endTimestamp, _stage, _submissionId, succeeded);
        }

        public void Dispose() => MSBuildEventSource.Log.EvaluateStop(_projectFile);
    }

    internal readonly struct EvaluationPassScope
    {
        private readonly string _projectFile;
        private readonly EvaluationPass _pass;
        private readonly ProjectEvaluationStage _stage;
        private readonly int _submissionId;
        private readonly long _metricsStartTimestamp;

        internal EvaluationPassScope(
            string projectFile,
            EvaluationPass pass,
            ProjectEvaluationStage stage,
            int submissionId,
            long metricsStartTimestamp)
        {
            _projectFile = projectFile;
            _pass = pass;
            _stage = stage;
            _submissionId = submissionId;
            _metricsStartTimestamp = metricsStartTimestamp;
        }

        internal void Complete() =>
            CompletePass(_projectFile, _pass, _stage, _submissionId, _metricsStartTimestamp);
    }

    /// <summary>Disables Metrics after an instrumentation failure so evaluation can continue safely.</summary>
    private static int s_metricsDisabled;

    private static long StartEvaluationMetrics()
    {
        if (Volatile.Read(ref s_metricsDisabled) != 0)
        {
            return 0;
        }

        try
        {
            return StartEvaluationMetricsCore();
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            DisableMetrics();
            return 0;
        }
    }

    // Keep Metrics type resolution inside the catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long StartEvaluationMetricsCore() =>
        Instruments.ProjectEvaluationDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    private static long StartPassMetrics()
    {
        if (Volatile.Read(ref s_metricsDisabled) != 0)
        {
            return 0;
        }

        try
        {
            return StartPassMetricsCore();
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            DisableMetrics();
            return 0;
        }
    }

    // Keep Metrics type resolution inside the catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long StartPassMetricsCore() =>
        Instruments.ProjectEvaluationPassDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    private static long GetMetricsEndTimestamp(long startTimestamp)
    {
        if (startTimestamp == 0 || Volatile.Read(ref s_metricsDisabled) != 0)
        {
            return 0;
        }

        try
        {
            return Stopwatch.GetTimestamp();
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            DisableMetrics();
            return 0;
        }
    }

    private static void RecordEvaluationMetrics(
        long startTimestamp,
        long endTimestamp,
        ProjectEvaluationStage stage,
        int submissionId,
        bool succeeded)
    {
        if (Volatile.Read(ref s_metricsDisabled) != 0)
        {
            return;
        }

        try
        {
            RecordEvaluationMetricsCore(startTimestamp, endTimestamp, stage, submissionId, succeeded);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            DisableMetrics();
        }
    }

    // Keep Metrics type resolution inside the catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RecordEvaluationMetricsCore(
        long startTimestamp,
        long endTimestamp,
        ProjectEvaluationStage stage,
        int submissionId,
        bool succeeded)
    {
        bool countEnabled = Instruments.ProjectEvaluationCount.Enabled;
        bool durationEnabled =
            startTimestamp != 0 &&
            endTimestamp != 0 &&
            Instruments.ProjectEvaluationDuration.Enabled;
        if (!countEnabled && !durationEnabled)
        {
            return;
        }

        TagList tags = default;
        tags.Add(StageTagName, GetStageName(stage));
        tags.Add(OriginTagName, GetOriginName(submissionId));
        tags.Add(SucceededTagName, succeeded);

        if (countEnabled)
        {
            Instruments.ProjectEvaluationCount.Add(1, in tags);
        }

        if (durationEnabled)
        {
            Instruments.ProjectEvaluationDuration.Record(GetElapsedSeconds(startTimestamp, endTimestamp), in tags);
        }
    }

    private static void RecordPassMetrics(
        long startTimestamp,
        long endTimestamp,
        EvaluationPass pass,
        ProjectEvaluationStage stage,
        int submissionId)
    {
        if (startTimestamp == 0 || endTimestamp == 0 || Volatile.Read(ref s_metricsDisabled) != 0)
        {
            return;
        }

        try
        {
            RecordPassMetricsCore(startTimestamp, endTimestamp, pass, stage, submissionId);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            DisableMetrics();
        }
    }

    // Keep Metrics type resolution inside the catch boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RecordPassMetricsCore(
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

        TagList tags = default;
        tags.Add(StageTagName, GetStageName(stage));
        tags.Add(PassTagName, GetPassName(pass));
        tags.Add(OriginTagName, GetOriginName(submissionId));

        Instruments.ProjectEvaluationPassDuration.Record(GetElapsedSeconds(startTimestamp, endTimestamp), in tags);
    }

    private static void DisableMetrics() => Volatile.Write(ref s_metricsDisabled, 1);

    private static string GetOriginName(int submissionId) =>
        submissionId != BuildEventContext.InvalidSubmissionId
            ? BuildSubmissionOrigin
            : OutsideBuildSubmissionOrigin;

    private static double GetElapsedSeconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;

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
