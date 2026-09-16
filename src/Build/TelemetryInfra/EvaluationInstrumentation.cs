// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal static class EvaluationInstrumentation
{
    private const string BuildSubmission = "build_submission";
    private const string OutsideBuildSubmission = "outside_build_submission";

    private static int s_disabled;

    internal static long StartMeasurement()
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return 0;
        }

        try
        {
            return IsEnabled() ? Stopwatch.GetTimestamp() : 0;
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable();
            return 0;
        }
    }

    internal static double EndPassMeasurement(long startTimestamp)
    {
        if (startTimestamp == 0 || Volatile.Read(ref s_disabled) != 0)
        {
            return double.NaN;
        }

        try
        {
            return IsEnabled() ? GetElapsedSeconds(startTimestamp) : double.NaN;
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable();
            return double.NaN;
        }
    }

    internal static void RecordPass(double durationSeconds, ProjectEvaluationStage stage, string pass, int submissionId)
    {
        if (double.IsNaN(durationSeconds) || Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
        {
            if (IsEnabled())
            {
                MSBuildEventSource.Log.ProjectEvaluationPassCompleted(
                    durationSeconds,
                    GetStage(stage),
                    pass,
                    GetOrigin(submissionId));
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable();
        }
    }

    internal static void RecordEvaluation(long startTimestamp, ProjectEvaluationStage stage, int submissionId, bool succeeded)
    {
        if (Volatile.Read(ref s_disabled) != 0)
        {
            return;
        }

        try
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
                succeeded);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            Disable();
        }
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

    private static void Disable() => Interlocked.Exchange(ref s_disabled, 1);
}
