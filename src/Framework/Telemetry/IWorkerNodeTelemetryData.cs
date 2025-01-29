// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework;

internal interface IWorkerNodeTelemetryData
{
    Dictionary<string, TaskExecutionStats> TasksExecutionData { get; }
    Dictionary<string, bool> TargetsExecutionData { get; }
}
