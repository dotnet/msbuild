// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.Tasks,
                    JsonSerializer.Serialize(telemetryData.TasksExecutionData, _serializerOptions), false));
            }

            if (includeTargetDetails)
            {
                telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.Targets,
                    JsonSerializer.Serialize(telemetryData.TargetsExecutionData, _serializerOptions), false));
            }

            TargetsSummaryConverter targetsSummary = new();
            targetsSummary.Process(telemetryData.TargetsExecutionData);
            telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.TargetsSummary,
                JsonSerializer.Serialize(targetsSummary, _serializerOptions), false));

            TasksSummaryConverter tasksSummary = new();
            tasksSummary.Process(telemetryData.TasksExecutionData);
            telemetryItems.Add(new TelemetryItem(NodeTelemetryTags.TasksSummary,
                JsonSerializer.Serialize(tasksSummary, _serializerOptions), false));

            return new NodeTelemetry(telemetryItems);
        }

        private static JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var opt = new JsonSerializerOptions
            {
                Converters =
                {
                    new TargetsDetailsConverter(),
                    new TasksDetailsConverter(),
                    new TargetsSummaryConverter(),
                    new TasksSummaryConverter(),
                },
            };

            return opt;
        }

        private class TargetsDetailsConverter : JsonConverter<Dictionary<TaskOrTargetTelemetryKey, bool>?>
        {
            public override Dictionary<TaskOrTargetTelemetryKey, bool>? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
                =>
                    throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                Dictionary<TaskOrTargetTelemetryKey, bool>? value,
                JsonSerializerOptions options)
            {
                if (value == null)
                {
                    throw new NotSupportedException("TaskOrTargetTelemetryKey cannot be null in telemetry data");
                }

                // Following needed - as System.Text.Json doesn't support indexing dictionary by composite types
                writer.WriteStartObject();

                foreach (KeyValuePair<TaskOrTargetTelemetryKey, bool> valuePair in value)
                {
                    string keyName = ShouldHashKey(valuePair.Key) ?
                        ActivityExtensions.GetHashed(valuePair.Key.Name) :
                        valuePair.Key.Name;

                    writer.WriteStartObject(keyName);
                    writer.WriteBoolean("WasExecuted", valuePair.Value);
                    writer.WriteBoolean(nameof(valuePair.Key.IsCustom), valuePair.Key.IsCustom);
                    writer.WriteBoolean(nameof(valuePair.Key.IsNuget), valuePair.Key.IsNuget);
                    writer.WriteBoolean(nameof(valuePair.Key.IsMetaProj), valuePair.Key.IsMetaProj);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            private bool ShouldHashKey(TaskOrTargetTelemetryKey key) => key.IsCustom || key.IsMetaProj;
        }

        private class TasksDetailsConverter : JsonConverter<Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>?>
        {
            public override Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
                =>
                    throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>? value,
                JsonSerializerOptions options)
            {
                if (value == null)
                {
                    throw new NotSupportedException("TaskOrTargetTelemetryKey cannot be null in telemetry data");
                }

                // Following needed - as System.Text.Json doesn't support indexing dictionary by composite types
                writer.WriteStartObject();

                foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> valuePair in value)
                {
                    string keyName = valuePair.Key.IsCustom ?
                        ActivityExtensions.GetHashed(valuePair.Key.Name) :
                        valuePair.Key.Name;
                    writer.WriteStartObject(keyName);
                    writer.WriteNumber(nameof(valuePair.Value.CumulativeExecutionTime.TotalMilliseconds), valuePair.Value.CumulativeExecutionTime.TotalMilliseconds);
                    writer.WriteNumber(nameof(valuePair.Value.ExecutionsCount), valuePair.Value.ExecutionsCount);
                    writer.WriteNumber(nameof(valuePair.Value.TotalMemoryBytes), valuePair.Value.TotalMemoryBytes);
                    writer.WriteBoolean(nameof(valuePair.Key.IsCustom), valuePair.Key.IsCustom);
                    writer.WriteBoolean(nameof(valuePair.Key.IsNuget), valuePair.Key.IsNuget);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }
        }

        private class TargetsSummaryConverter : JsonConverter<TargetsSummaryConverter>
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

            private TargetInfo LoadedBuiltinTargetInfo { get; } = new();
            private TargetInfo LoadedCustomTargetInfo { get; } = new();
            private TargetInfo ExecutedBuiltinTargetInfo { get; } = new();
            private TargetInfo ExecutedCustomTargetInfo { get; } = new();

            private class TargetInfo
            {
                public int Total { get; internal set; }
                public int FromNuget { get; internal set; }
                public int FromMetaproj { get; internal set; }
            }

            public override TargetsSummaryConverter? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
            throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                TargetsSummaryConverter value,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteStartObject("Loaded");
                WriteStat(writer, value.LoadedBuiltinTargetInfo, value.LoadedCustomTargetInfo);
                writer.WriteEndObject();
                writer.WriteStartObject("Executed");
                WriteStat(writer, value.ExecutedBuiltinTargetInfo, value.ExecutedCustomTargetInfo);
                writer.WriteEndObject();
                writer.WriteEndObject();

                void WriteStat(Utf8JsonWriter writer, TargetInfo builtinTargetsInfo, TargetInfo customTargetsInfo)
                {
                    writer.WriteNumber(nameof(builtinTargetsInfo.Total), builtinTargetsInfo.Total + customTargetsInfo.Total);
                    WriteSingleStat(writer, builtinTargetsInfo, "Microsoft");
                    WriteSingleStat(writer, customTargetsInfo, "Custom");
                }

                void WriteSingleStat(Utf8JsonWriter writer, TargetInfo targetInfo, string name)
                {
                    if (targetInfo.Total > 0)
                    {
                        writer.WriteStartObject(name);
                        writer.WriteNumber(nameof(targetInfo.Total), targetInfo.Total);
                        writer.WriteNumber(nameof(targetInfo.FromNuget), targetInfo.FromNuget);
                        writer.WriteNumber(nameof(targetInfo.FromMetaproj), targetInfo.FromMetaproj);
                        writer.WriteEndObject();
                    }
                }
            }
        }

        private class TasksSummaryConverter : JsonConverter<TasksSummaryConverter>
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

            private TasksInfo BuiltinTasksInfo { get; } = new TasksInfo();

            private TasksInfo CustomTasksInfo { get; } = new TasksInfo();

            private class TasksInfo
            {
                public TaskExecutionStats Total { get; } = TaskExecutionStats.CreateEmpty();

                public TaskExecutionStats FromNuget { get; } = TaskExecutionStats.CreateEmpty();
            }

            public override TasksSummaryConverter? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
            throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                TasksSummaryConverter value,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                WriteStat(writer, value.BuiltinTasksInfo, "Microsoft");
                WriteStat(writer, value.CustomTasksInfo, "Custom");
                writer.WriteEndObject();

                void WriteStat(Utf8JsonWriter writer, TasksInfo tasksInfo, string name)
                {
                    writer.WriteStartObject(name);
                    WriteSingleStat(writer, tasksInfo.Total, nameof(tasksInfo.Total));
                    WriteSingleStat(writer, tasksInfo.FromNuget, nameof(tasksInfo.FromNuget));
                    writer.WriteEndObject();
                }

                void WriteSingleStat(Utf8JsonWriter writer, TaskExecutionStats stats, string name)
                {
                    if (stats.ExecutionsCount > 0)
                    {
                        writer.WriteStartObject(name);
                        writer.WriteNumber(nameof(stats.ExecutionsCount), stats.ExecutionsCount);
                        writer.WriteNumber(nameof(stats.CumulativeExecutionTime.TotalMilliseconds), stats.CumulativeExecutionTime.TotalMilliseconds);
                        writer.WriteNumber(nameof(stats.TotalMemoryBytes), stats.TotalMemoryBytes);
                        writer.WriteEndObject();
                    }
                }
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
