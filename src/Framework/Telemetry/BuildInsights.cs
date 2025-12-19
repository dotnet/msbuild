// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using static Microsoft.Build.Framework.Telemetry.TelemetryDataUtils;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Container for all build telemetry insights including tasks and targets details and summaries.
/// </summary>
internal sealed class BuildInsights
{
    public List<TaskDetailInfo> Tasks { get; }

    public List<TargetDetailInfo> Targets { get; }

    public TargetsSummaryInfo TargetsSummary { get; }

    public TasksSummaryInfo TasksSummary { get; }

    public BuildInsights(
        List<TaskDetailInfo> tasks,
        List<TargetDetailInfo> targets,
        TargetsSummaryInfo targetsSummary,
        TasksSummaryInfo tasksSummary)
    {
        Tasks = tasks;
        Targets = targets;
        TargetsSummary = targetsSummary;
        TasksSummary = tasksSummary;
    }

    internal record TasksSummaryInfo(TaskCategoryStats? Microsoft, TaskCategoryStats? Custom);

    internal record TaskCategoryStats(TaskStatsInfo? Total, TaskStatsInfo? FromNuget);

    internal record TaskStatsInfo(int ExecutionsCount, double TotalMilliseconds, long TotalMemoryBytes);
}
