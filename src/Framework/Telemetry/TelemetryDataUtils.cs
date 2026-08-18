// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;

namespace Microsoft.Build.Framework.Telemetry
{
    internal static class TelemetryDataUtils
    {
        /// <summary>
        /// Known Microsoft task factory type names that should not be hashed.
        /// </summary>
        private static readonly HashSet<string> KnownTaskFactoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "AssemblyTaskFactory",
            "TaskHostFactory",
            "CodeTaskFactory",
            "RoslynCodeTaskFactory",
            "XamlTaskFactory",
            "IntrinsicTaskFactory",
        };

        /// <summary>
        /// Transforms collected telemetry data to format recognized by the telemetry infrastructure.
        /// </summary>
        /// <param name="telemetryData">Data about tasks and target forwarded from nodes.</param>
        /// <param name="includeTasksDetails">Controls whether Task details should attached to the telemetry.</param>
        /// <param name="includeTargetDetails">Controls whether Target details should be attached to the telemetry.</param>
        /// <returns>Node Telemetry data wrapped in <see cref="IActivityTelemetryDataHolder"/> a list of properties that can be attached as tags to a <see cref="IActivity"/>.</returns>
        public static IActivityTelemetryDataHolder? AsActivityDataHolder(this IWorkerNodeTelemetryData? telemetryData, bool includeTasksDetails, bool includeTargetDetails)
        {
            if (telemetryData == null)
            {
                return null;
            }

            var targetsSummary = new TargetsSummaryConverter();
            targetsSummary.Process(telemetryData.TargetsExecutionData);

            var tasksSummary = new TasksSummaryConverter();
            tasksSummary.Process(telemetryData.TasksExecutionData);

            var incrementality = ComputeIncrementalityInfo(telemetryData.TargetsExecutionData);

            var buildInsights = new BuildInsights(
                includeTasksDetails ? GetTasksDetails(telemetryData.TasksExecutionData) : [],
                includeTargetDetails ? GetTargetsDetails(telemetryData.TargetsExecutionData) : [],
                GetTargetsSummary(targetsSummary),
                GetTasksSummary(tasksSummary),
                incrementality);

            return new NodeTelemetry(buildInsights);
        }

        /// <summary>
        /// Converts targets details to a list of custom objects for telemetry.
        /// </summary>
        private static List<TargetDetailInfo> GetTargetsDetails(Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsDetails)
        {
            var result = new List<TargetDetailInfo>();

            foreach (KeyValuePair<TaskOrTargetTelemetryKey, TargetExecutionStats> valuePair in targetsDetails)
            {
                string targetName = ShouldHashKey(valuePair.Key) ? GetHashed(valuePair.Key.Name) : valuePair.Key.Name;

                result.Add(new TargetDetailInfo(
                    targetName,
                    valuePair.Value.WasExecuted,
                    valuePair.Key.IsCustom,
                    valuePair.Key.IsNuget,
                    valuePair.Key.IsMetaProj,
                    valuePair.Value.SkipReason));
            }

            return result;

            static bool ShouldHashKey(TaskOrTargetTelemetryKey key) => key.IsCustom || key.IsMetaProj;
        }

        internal record TargetDetailInfo(string Name, bool WasExecuted, bool IsCustom, bool IsNuget, bool IsMetaProj, TargetSkipReason SkipReason);

        /// <summary>
        /// Converts tasks details to a list of custom objects for telemetry.
        /// </summary>
        private static List<TaskDetailInfo> GetTasksDetails(
            Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksDetails)
        {
            var result = new List<TaskDetailInfo>();

            foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> valuePair in tasksDetails)
            {
                string taskName = valuePair.Key.IsCustom ? GetHashed(valuePair.Key.Name) : valuePair.Key.Name;
                string? factoryName = GetFactoryNameForTelemetry(valuePair.Value.TaskFactoryName);

                result.Add(new TaskDetailInfo(
                    taskName,
                    valuePair.Value.CumulativeExecutionTime.TotalMilliseconds,
                    valuePair.Value.ExecutionsCount,
                    valuePair.Value.TotalMemoryBytes,
                    valuePair.Key.IsCustom,
                    valuePair.Key.IsNuget,
                    factoryName,
                    valuePair.Value.TaskHostRuntime));
            }

            return result;
        }

        /// <summary>
        /// Gets the factory name for telemetry, hashing custom factory names.
        /// </summary>
        private static string? GetFactoryNameForTelemetry(string? factoryName)
        {
            if (string.IsNullOrEmpty(factoryName))
            {
                return null;
            }

            return KnownTaskFactoryNames.Contains(factoryName!) ? factoryName : GetHashed(factoryName!);
        }

        /// <summary>
        /// Depending on the platform, hash the value using an available mechanism.
        /// </summary>
        internal static string GetHashed(object value) => Sha256Hasher.Hash(value?.ToString() ?? "");

        // https://github.com/dotnet/sdk/blob/8bd19a2390a6bba4aa80d1ac3b6c5385527cc311/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs + workaround for netstandard2.0
        private static class Sha256Hasher
        {
            /// <summary>
            /// The hashed mac address needs to be the same hashed value as produced by the other distinct sources given the same input. (e.g. VsCode)
            /// </summary>
            public static string Hash(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
#if NET
                byte[] hash = SHA256.HashData(bytes);
#if NET9_0_OR_GREATER
                return System.Convert.ToHexStringLower(hash);
#else
                return Convert.ToHexString(hash).ToLowerInvariant();
#endif

#else
                // Create the SHA256 object and compute the hash
                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(bytes);

                    // Convert the hash bytes to a lowercase hex string (manual loop approach)
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                    {
                        sb.AppendFormat("{0:x2}", b);
                    }

                    return sb.ToString();
                }
#endif
            }
        }

        internal record TaskDetailInfo(string Name, double TotalMilliseconds, int ExecutionsCount, long TotalMemoryBytes, bool IsCustom, bool IsNuget, string? FactoryName, string? TaskHostRuntime);

        /// <summary>
        /// Converts targets summary to a custom object for telemetry.
        /// </summary>
        private static TargetsSummaryInfo GetTargetsSummary(TargetsSummaryConverter summary)
        {
            return new TargetsSummaryInfo(
                CreateTargetStats(summary.LoadedBuiltinTargetInfo, summary.LoadedCustomTargetInfo),
                CreateTargetStats(summary.ExecutedBuiltinTargetInfo, summary.ExecutedCustomTargetInfo));

            static TargetStatsInfo CreateTargetStats(
                TargetsSummaryConverter.TargetInfo builtinInfo,
                TargetsSummaryConverter.TargetInfo customInfo)
            {
                var microsoft = builtinInfo.Total > 0
                    ? new TargetCategoryInfo(builtinInfo.Total, builtinInfo.FromNuget, builtinInfo.FromMetaproj)
                    : null;

                var custom = customInfo.Total > 0
                    ? new TargetCategoryInfo(customInfo.Total, customInfo.FromNuget, customInfo.FromMetaproj)
                    : null;

                return new TargetStatsInfo(builtinInfo.Total + customInfo.Total, microsoft, custom);
            }
        }

        internal record TargetsSummaryInfo(TargetStatsInfo Loaded, TargetStatsInfo Executed);

        internal record TargetStatsInfo(int Total, TargetCategoryInfo? Microsoft, TargetCategoryInfo? Custom);

        internal record TargetCategoryInfo(int Total, int FromNuget, int FromMetaproj);

        /// <summary>
        /// Converts tasks summary to a custom object for telemetry.
        /// </summary>
        private static TasksSummaryInfo GetTasksSummary(TasksSummaryConverter summary)
        {
            var microsoft = CreateTaskStats(summary.BuiltinTasksInfo.Total, summary.BuiltinTasksInfo.FromNuget);
            var custom = CreateTaskStats(summary.CustomTasksInfo.Total, summary.CustomTasksInfo.FromNuget);

            return new TasksSummaryInfo(microsoft, custom);

            static TaskCategoryStats? CreateTaskStats(TaskExecutionStats total, TaskExecutionStats fromNuget)
            {
                var totalStats = total.ExecutionsCount > 0
                    ? new TaskStatsInfo(
                        total.ExecutionsCount,
                        total.CumulativeExecutionTime.TotalMilliseconds,
                        total.TotalMemoryBytes)
                    : null;

                var nugetStats = fromNuget.ExecutionsCount > 0
                    ? new TaskStatsInfo(
                        fromNuget.ExecutionsCount,
                        fromNuget.CumulativeExecutionTime.TotalMilliseconds,
                        fromNuget.TotalMemoryBytes)
                    : null;

                return (totalStats != null || nugetStats != null)
                    ? new TaskCategoryStats(totalStats, nugetStats)
                    : null;
            }
        }

        private class TargetsSummaryConverter
        {
            internal TargetInfo LoadedBuiltinTargetInfo { get; } = new();

            internal TargetInfo LoadedCustomTargetInfo { get; } = new();

            internal TargetInfo ExecutedBuiltinTargetInfo { get; } = new();

            internal TargetInfo ExecutedCustomTargetInfo { get; } = new();

            /// <summary>
            /// Processes target execution data to compile summary statistics for both built-in and custom targets.
            /// </summary>
            public void Process(Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsExecutionData)
            {
                foreach (var kv in targetsExecutionData)
                {
                    GetTargetInfo(kv.Key, isExecuted: false).Increment(kv.Key);

                    // Update executed targets statistics (only if executed)
                    if (kv.Value.WasExecuted)
                    {
                        GetTargetInfo(kv.Key, isExecuted: true).Increment(kv.Key);
                    }
                }
            }

            private TargetInfo GetTargetInfo(TaskOrTargetTelemetryKey key, bool isExecuted) =>
                (key.IsCustom, isExecuted) switch
                {
                    (true, true) => ExecutedCustomTargetInfo,
                    (true, false) => LoadedCustomTargetInfo,
                    (false, true) => ExecutedBuiltinTargetInfo,
                    (false, false) => LoadedBuiltinTargetInfo,
                };

            internal class TargetInfo
            {
                public int Total { get; private set; }

                public int FromNuget { get; private set; }

                public int FromMetaproj { get; private set; }

                internal void Increment(TaskOrTargetTelemetryKey key)
                {
                    Total++;
                    if (key.IsNuget)
                    {
                        FromNuget++;
                    }

                    if (key.IsMetaProj)
                    {
                        FromMetaproj++;
                    }
                }
            }
        }

        private class TasksSummaryConverter
        {
            internal TasksInfo BuiltinTasksInfo { get; } = new();

            internal TasksInfo CustomTasksInfo { get; } = new();

            /// <summary>
            /// Processes task execution data to compile summary statistics for both built-in and custom tasks.
            /// </summary>
            public void Process(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData)
            {
                foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> kv in tasksExecutionData)
                {
                    var taskInfo = kv.Key.IsCustom ? CustomTasksInfo : BuiltinTasksInfo;
                    taskInfo.Total.Accumulate(kv.Value);

                    if (kv.Key.IsNuget)
                    {
                        taskInfo.FromNuget.Accumulate(kv.Value);
                    }
                }
            }

            internal class TasksInfo
            {
                public TaskExecutionStats Total { get; } = TaskExecutionStats.CreateEmpty();

                public TaskExecutionStats FromNuget { get; } = TaskExecutionStats.CreateEmpty();
            }
        }

        /// <summary>
        /// Threshold ratio above which a build is classified as incremental.
        /// A build with more than 70% skipped targets is considered incremental.
        /// </summary>
        private const double IncrementalBuildThreshold = 0.70;

        /// <summary>
        /// Computes build incrementality information from target execution data.
        /// </summary>
        private static BuildInsights.BuildIncrementalityInfo ComputeIncrementalityInfo(
            Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats> targetsExecutionData)
        {
            int totalTargets = targetsExecutionData.Count;
            int executedTargets = 0;
            int skippedTargets = 0;
            int skippedDueToUpToDate = 0;
            int skippedDueToCondition = 0;
            int skippedDueToPreviouslyBuilt = 0;

            foreach (var kv in targetsExecutionData)
            {
                if (kv.Value.WasExecuted)
                {
                    executedTargets++;
                }
                else
                {
                    skippedTargets++;
                    _ = kv.Value.SkipReason switch
                    {
                        TargetSkipReason.OutputsUpToDate => skippedDueToUpToDate++,
                        TargetSkipReason.ConditionWasFalse => skippedDueToCondition++,
                        TargetSkipReason.PreviouslyBuiltSuccessfully or TargetSkipReason.PreviouslyBuiltUnsuccessfully => skippedDueToPreviouslyBuilt++,
                        _ => 0
                    };
                }
            }

            // Calculate incrementality ratio (0.0 = full build, 1.0 = fully incremental)
            double incrementalityRatio = totalTargets > 0 ? (double)skippedTargets / totalTargets : 0.0;

            var classification = totalTargets == 0
                ? BuildInsights.BuildType.Unknown
                : incrementalityRatio >= IncrementalBuildThreshold
                    ? BuildInsights.BuildType.Incremental
                    : BuildInsights.BuildType.Full;

            return new BuildInsights.BuildIncrementalityInfo(
                Classification: classification,
                TotalTargetsCount: totalTargets,
                ExecutedTargetsCount: executedTargets,
                SkippedTargetsCount: skippedTargets,
                SkippedDueToUpToDateCount: skippedDueToUpToDate,
                SkippedDueToConditionCount: skippedDueToCondition,
                SkippedDueToPreviouslyBuiltCount: skippedDueToPreviouslyBuilt,
                IncrementalityRatio: incrementalityRatio);
        }

        private sealed class NodeTelemetry(BuildInsights insights) : IActivityTelemetryDataHolder
        {
            Dictionary<string, object> IActivityTelemetryDataHolder.GetActivityProperties()
            {
                Dictionary<string, object> properties = new(5)
                {
                    [nameof(BuildInsights.TargetsSummary)] = insights.TargetsSummary,
                    [nameof(BuildInsights.TasksSummary)] = insights.TasksSummary,
                };

                AddIfNotEmpty(nameof(BuildInsights.Targets), insights.Targets);
                AddIfNotEmpty(nameof(BuildInsights.Tasks), insights.Tasks);
                AddIfNotNull(nameof(BuildInsights.Incrementality), insights.Incrementality);

                return properties;

                void AddIfNotEmpty<T>(string key, List<T> list)
                {
                    if (list.Count > 0)
                    {
                        properties[key] = list;
                    }
                }

                void AddIfNotNull(string key, object? value)
                {
                    if (value != null)
                    {
                        properties[key] = value;
                    }
                }
            }
        }
    }
}
