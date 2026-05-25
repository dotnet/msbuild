// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Framework.Telemetry;
using static Microsoft.Build.Framework.Telemetry.TelemetryDataUtils;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// Serializes per-task execution details into a TelemetryEventArgs-compatible
/// (<see cref="System.Collections.Generic.IDictionary{TKey, TValue}"/> of strings) shape for the CLI/SDK telemetry path.
///
/// Equivalent data is published to VS via <see cref="TelemetryDataUtils.AsActivityDataHolder"/>, where the
/// VS telemetry sink accepts arbitrary objects and serializes them itself. The CLI path requires strings
/// (see <see cref="Framework.TelemetryEventArgs"/>), so we serialize a JSON array here.
/// </summary>
internal static class TasksDetailsTelemetry
{
    /// <summary>
    /// Event name for per-task execution details emitted via TelemetryEventArgs.
    /// Uses a separate event name so per-task details are distinguishable from
    /// the per-project aggregated "build/tasks" telemetry event.
    /// </summary>
    internal const string TasksDetailsEventName = "build/tasks/details";

    /// <summary>
    /// Maximum number of individual task details to include in TelemetryEventArgs-based telemetry.
    /// </summary>
    private const int MaxTaskDetailsForTelemetryEvent = 100;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static Dictionary<string, string>? GetTasksDetailsProperties(this IWorkerNodeTelemetryData? telemetryData)
    {
        if (telemetryData is null || telemetryData.TasksExecutionData.Count == 0)
        {
            return null;
        }

        List<TaskDetailInfo> allTasks = GetTasksDetails(telemetryData.TasksExecutionData);
        List<TaskDetailInfo> topTasks = allTasks
            .OrderByDescending(t => t.ExecutionsCount)
            .Take(MaxTaskDetailsForTelemetryEvent)
            .ToList();

        return new Dictionary<string, string>(3)
        {
            ["TaskCount"] = topTasks.Count.ToString(CultureInfo.InvariantCulture),
            ["TotalTaskCount"] = allTasks.Count.ToString(CultureInfo.InvariantCulture),
            ["Tasks"] = JsonSerializer.Serialize(topTasks, s_jsonOptions),
        };
    }
}
