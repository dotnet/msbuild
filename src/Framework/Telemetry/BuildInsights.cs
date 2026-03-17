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

    /// <summary>
    /// Information about build incrementality classification.
    /// </summary>
    public BuildIncrementalityInfo? Incrementality { get; }

    public BuildInsights(
        List<TaskDetailInfo> tasks,
        List<TargetDetailInfo> targets,
        TargetsSummaryInfo targetsSummary,
        TasksSummaryInfo tasksSummary,
        BuildIncrementalityInfo? incrementality = null)
    {
        Tasks = tasks;
        Targets = targets;
        TargetsSummary = targetsSummary;
        TasksSummary = tasksSummary;
        Incrementality = incrementality;
    }

    internal record TasksSummaryInfo(TaskCategoryStats? Microsoft, TaskCategoryStats? Custom);

    internal record TaskCategoryStats(TaskStatsInfo? Total, TaskStatsInfo? FromNuget);

    internal record TaskStatsInfo(int ExecutionsCount, double TotalMilliseconds, long TotalMemoryBytes);

    internal record ErrorCountsInfo(
        int? Compiler,
        int? MsBuildGeneral,
        int? MsBuildEvaluation,
        int? MsBuildExecution,
        int? MsBuildGraph,
        int? Task,
        int? SdkResolvers,
        int? NetSdk,
        int? NuGet,
        int? BuildCheck,
        int? NativeToolchain,
        int? CodeAnalysis,
        int? Razor,
        int? Wpf,
        int? AspNet,
        int? Other);

    /// <summary>
    /// Represents the type of build based on incrementality analysis.
    /// </summary>
    internal enum BuildType
    {
        /// <summary>
        /// Build type could not be determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// Full build where most targets were executed.
        /// </summary>
        Full,

        /// <summary>
        /// Incremental build where most targets were skipped due to up-to-date checks.
        /// </summary>
        Incremental
    }

    /// <summary>
    /// Information about build incrementality classification.
    /// </summary>
    /// <param name="Classification">The determined build type (Full, Incremental, or Unknown).</param>
    /// <param name="TotalTargetsCount">Total number of targets in the build.</param>
    /// <param name="ExecutedTargetsCount">Number of targets that were actually executed.</param>
    /// <param name="SkippedTargetsCount">Number of targets that were skipped.</param>
    /// <param name="SkippedDueToUpToDateCount">Number of targets skipped because outputs were up-to-date.</param>
    /// <param name="SkippedDueToConditionCount">Number of targets skipped due to false conditions.</param>
    /// <param name="SkippedDueToPreviouslyBuiltCount">Number of targets skipped because they were previously built.</param>
    /// <param name="IncrementalityRatio">Ratio of skipped targets to total targets (0.0 to 1.0). Higher values indicate more incremental builds.</param>
    internal record BuildIncrementalityInfo(
        BuildType Classification,
        int TotalTargetsCount,
        int ExecutedTargetsCount,
        int SkippedTargetsCount,
        int SkippedDueToUpToDateCount,
        int SkippedDueToConditionCount,
        int SkippedDueToPreviouslyBuiltCount,
        double IncrementalityRatio);
}
