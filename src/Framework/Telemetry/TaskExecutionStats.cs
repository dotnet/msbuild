// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

internal class TaskExecutionStats(TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumption)
{
    private TaskExecutionStats()
        : this(TimeSpan.Zero, 0, 0)
    { }

    internal static TaskExecutionStats CreateEmpty()
        => new();

    /// <summary>
    /// Total execution time of the task in all nodes for all projects.
    /// </summary>
    public TimeSpan CumulativeExecutionTime { get; set; } = cumulativeExecutionTime;

    /// <summary>
    /// Total memory consumption (across all executions) in bytes.
    /// </summary>
    public long TotalMemoryConsumption { get; set; } = totalMemoryConsumption;

    /// <summary>
    /// Total number of execution of the tasks in all nodes for all projects.
    /// </summary>
    public int ExecutionsCount { get; set; } = executionsCount;

    internal void AddAnother(TaskExecutionStats another)
    {
        this.CumulativeExecutionTime += another.CumulativeExecutionTime;
        this.TotalMemoryConsumption += another.TotalMemoryConsumption;
        this.ExecutionsCount += another.ExecutionsCount;
    }

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

    // Needed since we override Equals
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
