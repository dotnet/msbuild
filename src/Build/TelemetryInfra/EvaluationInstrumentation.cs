// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal static class EvaluationInstrumentation
{
    private const string BuildSubmission = "build_submission";
    private const string OutsideBuildSubmission = "outside_build_submission";

    internal static long StartMeasurement()
        => IsEnabled() ? Stopwatch.GetTimestamp() : 0;

    internal static double EndPassMeasurement(long startTimestamp)
    {
        if (startTimestamp == 0)
        {
            return double.NaN;
        }

        return IsEnabled() ? GetElapsedSeconds(startTimestamp) : double.NaN;
    }

    internal static void RecordPass(
        double durationSeconds,
        ProjectEvaluationStage stage,
        string pass,
        int submissionId,
        string projectFile,
        int evaluationId)
    {
        if (double.IsNaN(durationSeconds))
        {
            return;
        }

        if (IsEnabled())
        {
            MSBuildEventSource.Log.ProjectEvaluationPassCompleted(
                durationSeconds,
                GetStage(stage),
                pass,
                GetOrigin(submissionId),
                projectFile,
                evaluationId);
        }
    }

    internal static void RecordEvaluation(
        long startTimestamp,
        ProjectEvaluationStage stage,
        int submissionId,
        bool succeeded,
        string projectFile,
        int evaluationId)
    {
        if (!IsEnabled())
        {
            return;
        }

        double durationSeconds = startTimestamp == 0
            ? double.NaN
            : GetElapsedSeconds(startTimestamp);

        MSBuildEventSource.Log.ProjectEvaluationCompleted(
            durationSeconds,
            GetStage(stage),
            GetOrigin(submissionId),
            succeeded,
            projectFile,
            evaluationId);
    }

    private static bool IsEnabled()
        => MSBuildEventSource.Log.IsEnabled(EventLevel.Informational, MSBuildEventSource.Keywords.EvaluationMeasurements);

    private static string GetStage(ProjectEvaluationStage stage) => stage switch
    {
        ProjectEvaluationStage.Properties => "properties",
        ProjectEvaluationStage.ItemDefinitions => "item_definitions",
        ProjectEvaluationStage.Items => "items",
        ProjectEvaluationStage.UsingTasks => "using_tasks",
        ProjectEvaluationStage.Full => "full",
        _ => "unknown",
    };

    private static string GetOrigin(int submissionId)
        => submissionId == BuildEventContext.InvalidSubmissionId ? OutsideBuildSubmission : BuildSubmission;

    private static double GetElapsedSeconds(long startTimestamp)
        => (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
}
