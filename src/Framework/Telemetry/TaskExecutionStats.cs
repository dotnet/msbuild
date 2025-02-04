// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

internal class TaskExecutionStats(TimeSpan cumulativeExecutionTime, short executionsCount, long totalMemoryConsumption)
{
    public TimeSpan CumulativeExecutionTime { get; set; } = cumulativeExecutionTime;
    public long TotalMemoryConsumption { get; set; } = totalMemoryConsumption;
    public short ExecutionsCount { get; set; } = executionsCount;

    // We need custom Equals for easier assertations in tests
    public override bool Equals(object? obj)
    {
        if (obj is TaskExecutionStats other)
        {
            return Equals(other);
        }
        return false;
    }

    protected bool Equals(TaskExecutionStats other)
        => CumulativeExecutionTime.Equals(other.CumulativeExecutionTime) &&
           TotalMemoryConsumption == other.TotalMemoryConsumption &&
           ExecutionsCount == other.ExecutionsCount;

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = CumulativeExecutionTime.GetHashCode();
            hashCode = (hashCode * 397) ^ TotalMemoryConsumption.GetHashCode();
            hashCode = (hashCode * 397) ^ ExecutionsCount.GetHashCode();
            return hashCode;
        }
    }
}
