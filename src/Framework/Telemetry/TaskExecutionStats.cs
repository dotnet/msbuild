// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

internal class TaskExecutionStats(TimeSpan cumulativeExecutionTime, short executionsCount, long totalMemoryConsumption)
{
    public TimeSpan CumulativeExecutionTime { get; set; } = cumulativeExecutionTime;
    public long TotalMemoryConsumption { get; set; } = totalMemoryConsumption;
    public short ExecutionsCount { get; set; } = executionsCount;
}
