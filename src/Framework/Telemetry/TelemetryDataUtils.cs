// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if NETFRAMEWORK

using System.Collections.Generic;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal static class TelemetryDataUtils
    {
        /// <summary>
        /// Transforms collected telemetry data to format recognized by the telemetry infrastructure.
        /// </summary>
        /// <param name="telemetryData">Data about tasks and target forwarded from nodes.</param>
        /// <param name="includeTasksDetails">Controls whether Task details should attached to the telemetry.</param>
        /// <param name="includeTargetDetails">Controls whether Target details should be attached to the telemetry.</param>
        /// <returns>Node Telemetry data wrapped in <see cref="IActivityTelemetryDataHolder"/> a list of properties that can be attached as tags to a <see cref="System.Diagnostics.Activity"/>.</returns>
        public static IActivityTelemetryDataHolder? AsActivityDataHolder(this IWorkerNodeTelemetryData? telemetryData, bool includeTasksDetails, bool includeTargetDetails)
        {
            if (telemetryData == null)
            {
                return null;
            }

            List<TelemetryItem> telemetryItems = new(4);

            if (includeTasksDetails)
            {
                telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.Tasks, ConvertTasksDetailsToPropertyBag(telemetryData.TasksExecutionData)));
            }

            if (includeTargetDetails)
            {
                telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.Targets, ConvertTargetsDetailsToPropertyBag(telemetryData.TargetsExecutionData)));
            }

            TargetsSummaryConverter targetsSummary = new();
            targetsSummary.Process(telemetryData.TargetsExecutionData);
            telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.TargetsSummary, new TelemetryComplexProperty(ConvertTargetsSummaryToPropertyBag(targetsSummary))));

            TasksSummaryConverter tasksSummary = new();
            tasksSummary.Process(telemetryData.TasksExecutionData);
            telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.TasksSummary, new TelemetryComplexProperty(ConvertTasksSummaryToPropertyBag(tasksSummary))));

            return new NodeTelemetry(telemetryItems);
        }

        /// <summary>
        /// Converts targets details to a property bag (dictionary) for telemetry.
        /// </summary>
        private static Dictionary<string, object> ConvertTargetsDetailsToPropertyBag(
            Dictionary<TaskOrTargetTelemetryKey, bool> targetsDetails)
        {
            var result = new Dictionary<string, object>();

            foreach (KeyValuePair<TaskOrTargetTelemetryKey, bool> valuePair in targetsDetails)
            {
                string keyName = ShouldHashKey(valuePair.Key) ?
                    ActivityExtensions.GetHashed(valuePair.Key.Name) :
                    valuePair.Key.Name;

                result[keyName] = new Dictionary<string, object>
                {
                    ["WasExecuted"] = valuePair.Value,
                    ["IsCustom"] = valuePair.Key.IsCustom,
                    ["IsNuget"] = valuePair.Key.IsNuget,
                    ["IsMetaProj"] = valuePair.Key.IsMetaProj
                };
            }

            return result;

            static bool ShouldHashKey(TaskOrTargetTelemetryKey key) => key.IsCustom || key.IsMetaProj;
        }

        /// <summary>
        /// Converts tasks details to a property bag (dictionary) for telemetry.
        /// </summary>
        private static Dictionary<string, object> ConvertTasksDetailsToPropertyBag(
            Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksDetails)
        {
            var result = new Dictionary<string, object>();

            foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> valuePair in tasksDetails)
            {
                string keyName = valuePair.Key.IsCustom ?
                    ActivityExtensions.GetHashed(valuePair.Key.Name) :
                    valuePair.Key.Name;

                result[keyName] = new Dictionary<string, object>
                {
                    ["TotalMilliseconds"] = valuePair.Value.CumulativeExecutionTime.TotalMilliseconds,
                    ["ExecutionsCount"] = valuePair.Value.ExecutionsCount,
                    ["TotalMemoryBytes"] = valuePair.Value.TotalMemoryBytes,
                    ["IsCustom"] = valuePair.Key.IsCustom,
                    ["IsNuget"] = valuePair.Key.IsNuget
                };
            }

            return result;
        }

        /// <summary>
        /// Converts targets summary to a property bag (dictionary) for telemetry.
        /// </summary>
        private static Dictionary<string, object> ConvertTargetsSummaryToPropertyBag(TargetsSummaryConverter summary)
        {
            return new Dictionary<string, object>
            {
                ["Loaded"] = CreateTargetStats(
                    summary.LoadedBuiltinTargetInfo,
                    summary.LoadedCustomTargetInfo),
                ["Executed"] = CreateTargetStats(
                    summary.ExecutedBuiltinTargetInfo,
                    summary.ExecutedCustomTargetInfo)
            };

            static Dictionary<string, object> CreateTargetStats(
                TargetsSummaryConverter.TargetInfo builtinInfo,
                TargetsSummaryConverter.TargetInfo customInfo)
            {
                var stats = new Dictionary<string, object>
                {
                    ["Total"] = builtinInfo.Total + customInfo.Total
                };

                if (builtinInfo.Total > 0)
                {
                    stats["Microsoft"] = new Dictionary<string, object>
                    {
                        ["Total"] = builtinInfo.Total,
                        ["FromNuget"] = builtinInfo.FromNuget,
                        ["FromMetaproj"] = builtinInfo.FromMetaproj
                    };
                }

                if (customInfo.Total > 0)
                {
                    stats["Custom"] = new Dictionary<string, object>
                    {
                        ["Total"] = customInfo.Total,
                        ["FromNuget"] = customInfo.FromNuget,
                        ["FromMetaproj"] = customInfo.FromMetaproj
                    };
                }

                return stats;
            }
        }

        /// <summary>
        /// Converts tasks summary to a property bag (dictionary) for telemetry.
        /// </summary>
        private static Dictionary<string, object> ConvertTasksSummaryToPropertyBag(TasksSummaryConverter summary)
        {
            var result = new Dictionary<string, object>();

            var microsoftDict = new Dictionary<string, object>();
            AddStatsIfNotEmpty(microsoftDict, "Total", summary.BuiltinTasksInfo.Total);
            AddStatsIfNotEmpty(microsoftDict, "FromNuget", summary.BuiltinTasksInfo.FromNuget);
            if (microsoftDict.Count > 0)
            {
                result["Microsoft"] = microsoftDict;
            }

            var customDict = new Dictionary<string, object>();
            AddStatsIfNotEmpty(customDict, "Total", summary.CustomTasksInfo.Total);
            AddStatsIfNotEmpty(customDict, "FromNuget", summary.CustomTasksInfo.FromNuget);
            if (customDict.Count > 0)
            {
                result["Custom"] = customDict;
            }

            return result;

            static void AddStatsIfNotEmpty(Dictionary<string, object> parent, string key, TaskExecutionStats stats)
            {
                if (stats.ExecutionsCount > 0)
                {
                    parent[key] = new Dictionary<string, object>
                    {
                        ["ExecutionsCount"] = stats.ExecutionsCount,
                        ["TotalMilliseconds"] = stats.CumulativeExecutionTime.TotalMilliseconds,
                        ["TotalMemoryBytes"] = stats.TotalMemoryBytes
                    };
                }
            }
        }

        private class TargetsSummaryConverter
        {
            /// <summary>
            /// Processes target execution data to compile summary statistics for both built-in and custom targets.
            /// </summary>
            /// <param name="targetsExecutionData">Dictionary containing target execution data keyed by task identifiers.</param>
            public void Process(Dictionary<TaskOrTargetTelemetryKey, bool> targetsExecutionData)
            {
                foreach (KeyValuePair<TaskOrTargetTelemetryKey, bool> targetPair in targetsExecutionData)
                {
                    TaskOrTargetTelemetryKey key = targetPair.Key;
                    bool wasExecuted = targetPair.Value;

                    // Update loaded targets statistics (all targets are loaded)
                    UpdateTargetStatistics(key, isExecuted: false);

                    // Update executed targets statistics (only targets that were actually executed)
                    if (wasExecuted)
                    {
                        UpdateTargetStatistics(key, isExecuted: true);
                    }
                }
            }

            private void UpdateTargetStatistics(TaskOrTargetTelemetryKey key, bool isExecuted)
            {
                // Select the appropriate target info collections based on execution state
                TargetInfo builtinTargetInfo = isExecuted ? ExecutedBuiltinTargetInfo : LoadedBuiltinTargetInfo;
                TargetInfo customTargetInfo = isExecuted ? ExecutedCustomTargetInfo : LoadedCustomTargetInfo;

                // Update either custom or builtin target info based on target type
                TargetInfo targetInfo = key.IsCustom ? customTargetInfo : builtinTargetInfo;

                targetInfo.Total++;
                if (key.IsNuget)
                {
                    targetInfo.FromNuget++;
                }
                if (key.IsMetaProj)
                {
                    targetInfo.FromMetaproj++;
                }
            }

            internal TargetInfo LoadedBuiltinTargetInfo { get; } = new();

            internal TargetInfo LoadedCustomTargetInfo { get; } = new();

            internal TargetInfo ExecutedBuiltinTargetInfo { get; } = new();

            internal TargetInfo ExecutedCustomTargetInfo { get; } = new();

            internal class TargetInfo
            {
                public int Total { get; internal set; }

                public int FromNuget { get; internal set; }

                public int FromMetaproj { get; internal set; }
            }
        }

        private class TasksSummaryConverter
        {
            /// <summary>
            /// Processes task execution data to compile summary statistics for both built-in and custom tasks.
            /// </summary>
            /// <param name="tasksExecutionData">Dictionary containing task execution data keyed by task identifiers.</param>
            public void Process(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData)
            {
                foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> taskInfo in tasksExecutionData)
                {
                    UpdateTaskStatistics(BuiltinTasksInfo, CustomTasksInfo, taskInfo.Key, taskInfo.Value);
                }
            }

            private void UpdateTaskStatistics(
                TasksInfo builtinTaskInfo,
                TasksInfo customTaskInfo,
                TaskOrTargetTelemetryKey key,
                TaskExecutionStats taskExecutionStats)
            {
                TasksInfo taskInfo = key.IsCustom ? customTaskInfo : builtinTaskInfo;
                taskInfo.Total.Accumulate(taskExecutionStats);

                if (key.IsNuget)
                {
                    taskInfo.FromNuget.Accumulate(taskExecutionStats);
                }
            }

            internal TasksInfo BuiltinTasksInfo { get; } = new TasksInfo();

            internal TasksInfo CustomTasksInfo { get; } = new TasksInfo();

            internal class TasksInfo
            {
                public TaskExecutionStats Total { get; } = TaskExecutionStats.CreateEmpty();

                public TaskExecutionStats FromNuget { get; } = TaskExecutionStats.CreateEmpty();
            }
        }

        private class NodeTelemetry : IActivityTelemetryDataHolder
        {
            private readonly IList<TelemetryItem> _items;

            public NodeTelemetry(IList<TelemetryItem> items) => _items = items;

            public IList<TelemetryItem> GetActivityProperties()
                => _items;
        }
    }
}

#endif
