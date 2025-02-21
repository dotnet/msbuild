// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework.Telemetry
{
    internal static class TelemetryDataUtils
    {
        public static IActivityTelemetryDataHolder? AsActivityDataHolder(this IWorkerNodeTelemetryData? telemetryData)
        {
            if (telemetryData == null)
            {
                return null;
            }

            List<TelemetryItem> telemetryItems = new(2);

            telemetryItems.Add(new TelemetryItem("Tasks",
                JsonSerializer.Serialize(telemetryData.TasksExecutionData, _serializerOptions), false));
            telemetryItems.Add(new TelemetryItem("Targets",
                JsonSerializer.Serialize(telemetryData.TargetsExecutionData, _serializerOptions), false));

            return new NodeTelemetry(telemetryItems);
        }

        private static JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var opt = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
                {
                    new TargetDataConverter(),
                    new TaskDataConverter(),
                },
                // TypeInfoResolver = new PrivateConstructorContractResolver()
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
                    writer.WriteStartObject(valuePair.Key.IsCustom ? ActivityExtensions.GetHashed(valuePair.Key.Name) : valuePair.Key.Name);
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
                    writer.WriteNumber("ExecTimeMs", valuePair.Value.CumulativeExecutionTime.TotalMilliseconds);
                    writer.WriteNumber("ExecCnt", valuePair.Value.ExecutionsCount);
                    writer.WriteNumber("MemKBs", valuePair.Value.TotalMemoryConsumption / 1024.0);
                    writer.WriteBoolean("IsCustom", valuePair.Key.IsCustom);
                    writer.WriteBoolean("IsFromNuget", valuePair.Key.IsFromNugetCache);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
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
