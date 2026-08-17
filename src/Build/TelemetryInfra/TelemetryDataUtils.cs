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
        public static IActivityTelemetryDataHolder? AsActivityDataHolder(this IWorkerNodeTelemetryData? telemetryData, bool includeTasksDetails, bool includeTargetDetails)
        {
            if (telemetryData == null)
            {
                return null;
            }

            List<TelemetryItem> telemetryItems = new(4);

            if (includeTasksDetails)
            {
                telemetryItems.Add(new TelemetryItem("Tasks",
                    JsonSerializer.Serialize(telemetryData.TasksExecutionData, _serializerOptions), false));
            }

            if (includeTargetDetails)
            {
                telemetryItems.Add(new TelemetryItem("Targets",
                    JsonSerializer.Serialize(telemetryData.TargetsExecutionData, _serializerOptions), false));
            }

            TargetsSummary targetsSummary = new();
            targetsSummary.Initialize(telemetryData.TargetsExecutionData);
            telemetryItems.Add(new TelemetryItem("TargetsSummary",
                JsonSerializer.Serialize(targetsSummary, _serializerOptions), false));

            TasksSummary tasksSummary = new();
            tasksSummary.Initialize(telemetryData.TasksExecutionData);
            telemetryItems.Add(new TelemetryItem("TasksSummary",
                JsonSerializer.Serialize(tasksSummary, _serializerOptions), false));

            return new NodeTelemetry(telemetryItems);
        }

        private static JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var opt = new JsonSerializerOptions
            {
                // Add following if user-friendly indentation would be needed
                // WriteIndented = true,
                Converters =
                {
                    new TargetDataConverter(),
                    new TaskDataConverter(),
                    new TargetsSummary(),
                    new TasksSummary(),
                },
            };

            return opt;
        }

        private class TargetDataConverter : JsonConverter<Dictionary<TaskOrTargetTelemetryKey, bool>?>
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

                writer.WriteStartArray();

                foreach (KeyValuePair<TaskOrTargetTelemetryKey, bool> valuePair in value)
                {
                    writer.WriteStartObject(valuePair.Key.IsCustom || valuePair.Key.IsFromMetaProject ? ActivityExtensions.GetHashed(valuePair.Key.Name) : valuePair.Key.Name);
                    writer.WriteBoolean("WasExecuted", valuePair.Value);
                    writer.WriteBoolean("IsCustom", valuePair.Key.IsCustom);
                    writer.WriteBoolean("IsFromNuget", valuePair.Key.IsFromNugetCache);
                    writer.WriteBoolean("IsMetaproj", valuePair.Key.IsFromMetaProject);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
        }

        private class TaskDataConverter : JsonConverter<Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>?>
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

                writer.WriteStartArray();

                foreach (KeyValuePair<TaskOrTargetTelemetryKey, TaskExecutionStats> valuePair in value)
                {
                    writer.WriteStartObject(valuePair.Key.IsCustom ? ActivityExtensions.GetHashed(valuePair.Key.Name) : valuePair.Key.Name);
                    // We do not want decimals
                    writer.WriteNumber("ExecTimeMs", valuePair.Value.CumulativeExecutionTime.TotalMilliseconds / 1);
                    writer.WriteNumber("ExecCnt", valuePair.Value.ExecutionsCount);
                    // We do not want decimals
                    writer.WriteNumber("MemKBs", valuePair.Value.TotalMemoryConsumption / 1024);
                    writer.WriteBoolean("IsCustom", valuePair.Key.IsCustom);
                    writer.WriteBoolean("IsFromNuget", valuePair.Key.IsFromNugetCache);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
        }

        private class TargetsSummary : JsonConverter<TargetsSummary>
        {
            public void Initialize(Dictionary<TaskOrTargetTelemetryKey, bool> targetsExecutionData)
            {
                foreach (var targetInfo in targetsExecutionData)
                {
                    UpdateStatistics(LoadedBuiltinTargetInfo, LoadedCustomTargetInfo, targetInfo.Key);
                    if (targetInfo.Value)
                    {
                        UpdateStatistics(ExecutedBuiltinTargetInfo, ExecutedCustomTargetInfo, targetInfo.Key);
                    }
                }

                void UpdateStatistics(
                    TargetInfo builtinTargetInfo,
                    TargetInfo customTargetInfo,
                    TaskOrTargetTelemetryKey key)
                {
                    UpdateSingleStatistics(key.IsCustom ? customTargetInfo : builtinTargetInfo, key);

                    void UpdateSingleStatistics(TargetInfo targetInfo, TaskOrTargetTelemetryKey kkey)
                    {
                        targetInfo.Total++;
                        if (kkey.IsFromNugetCache)
                        {
                            targetInfo.FromNuget++;
                        }
                        if (kkey.IsFromMetaProject)
                        {
                            targetInfo.FromMetaproj++;
                        }
                    }
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

            public override TargetsSummary? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
            throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                TargetsSummary value,
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


                void WriteStat(Utf8JsonWriter writer, TargetInfo customTargetsInfo, TargetInfo builtinTargetsInfo)
                {
                    writer.WriteNumber("Total", builtinTargetsInfo.Total + customTargetsInfo.Total);
                    WriteSingleStat(writer, builtinTargetsInfo, "Microsoft");
                    WriteSingleStat(writer, customTargetsInfo, "Custom");
                }

                void WriteSingleStat(Utf8JsonWriter writer, TargetInfo targetInfo, string name)
                {
                    if (targetInfo.Total > 0)
                    {
                        writer.WriteStartObject(name);
                        writer.WriteNumber("Total", targetInfo.Total);
                        writer.WriteNumber("FromNuget", targetInfo.FromNuget);
                        writer.WriteNumber("FromMetaproj", targetInfo.FromMetaproj);
                        writer.WriteEndObject();
                    }
                }
            }
        }


        private class TasksSummary : JsonConverter<TasksSummary>
        {
            public void Initialize(Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> tasksExecutionData)
            {
                foreach (var taskInfo in tasksExecutionData)
                {
                    UpdateStatistics(BuiltinTasksInfo, CustomTasksInfo, taskInfo.Key, taskInfo.Value);
                }

                void UpdateStatistics(
                    TasksInfo builtinTaskInfo,
                    TasksInfo customTaskInfo,
                    TaskOrTargetTelemetryKey key,
                    TaskExecutionStats taskExecutionStats)
                {
                    UpdateSingleStatistics(key.IsCustom ? customTaskInfo : builtinTaskInfo, taskExecutionStats, key);

                    void UpdateSingleStatistics(TasksInfo summarizedTaskInfo, TaskExecutionStats infoToAdd, TaskOrTargetTelemetryKey kkey)
                    {
                        summarizedTaskInfo.Total.AddAnother(infoToAdd);
                        if (kkey.IsFromNugetCache)
                        {
                            summarizedTaskInfo.FromNuget.AddAnother(infoToAdd);
                        }
                    }
                }
            }

            private TasksInfo BuiltinTasksInfo { get; } = new TasksInfo();
            private TasksInfo CustomTasksInfo { get; } = new TasksInfo();

            private class TasksInfo
            {
                public TaskExecutionStats Total { get; } = TaskExecutionStats.CreateEmpty();
                public TaskExecutionStats FromNuget { get; } = TaskExecutionStats.CreateEmpty();
            }

            public override TasksSummary? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
            throw new NotImplementedException("Reading is not supported");

            public override void Write(
                Utf8JsonWriter writer,
                TasksSummary value,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                WriteStat(writer, value.BuiltinTasksInfo, "Microsoft");
                WriteStat(writer, value.CustomTasksInfo, "Custom");
                writer.WriteEndObject();

                void WriteStat(Utf8JsonWriter writer, TasksInfo tasksInfo, string name)
                {
                    writer.WriteStartObject(name);
                    WriteSingleStat(writer, tasksInfo.Total, "Total", true);
                    WriteSingleStat(writer, tasksInfo.FromNuget, "FromNuget", false);
                    writer.WriteEndObject();
                }

                void WriteSingleStat(Utf8JsonWriter writer, TaskExecutionStats stats, string name, bool writeIfEmpty)
                {
                    if (stats.ExecutionsCount > 0)
                    {
                        writer.WriteStartObject(name);
                        writer.WriteNumber("TotalExecutionsCount", stats.ExecutionsCount);
                        // We do not want decimals
                        writer.WriteNumber("CumulativeExecutionTimeMs", (long)stats.CumulativeExecutionTime.TotalMilliseconds);
                        // We do not want decimals
                        writer.WriteNumber("CumulativeConsumedMemoryKB", stats.TotalMemoryConsumption / 1024);
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
