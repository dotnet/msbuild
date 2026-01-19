// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal interface IWorkerNodeTelemetryData
{
    Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats> TasksExecutionData { get; }

    Dictionary<TaskOrTargetTelemetryKey, bool> TargetsExecutionData { get; }
}
