// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Represents the execution statistics of tasks executed on a node.
/// </summary>
internal class TaskExecutionStats(TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumption)
{
    private TaskExecutionStats()
        : this(TimeSpan.Zero, 0, 0)
    { }

    /// <summary>
    /// Creates an instance of <see cref="TaskExecutionStats"/> initialized to zero values.
    /// </summary>
    /// <returns>Empty task execution statistics.</returns>
    internal static TaskExecutionStats CreateEmpty()
        => new();

    /// <summary>
    /// Total execution time of the task in all nodes for all projects.
    /// </summary>
    public TimeSpan CumulativeExecutionTime { get; set; } = cumulativeExecutionTime;

    /// <summary>
    /// Total memory consumption (across all executions) in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; set; } = totalMemoryConsumption;

    /// <summary>
    /// Total number of executions of the task.
    /// </summary>
    public int ExecutionsCount { get; set; } = executionsCount;

    /// <summary>
    /// Accumulates statistics from another instance into this one.
    /// </summary>
    /// <param name="other">Statistics to add to this instance.</param>
    internal void Accumulate(TaskExecutionStats other)
    {
        this.CumulativeExecutionTime += other.CumulativeExecutionTime;
        this.TotalMemoryBytes += other.TotalMemoryBytes;
        this.ExecutionsCount += other.ExecutionsCount;
    }

    // We need custom Equals for easier assertions in tests
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
           TotalMemoryBytes == other.TotalMemoryBytes &&
           ExecutionsCount == other.ExecutionsCount;

    // Needed since we override Equals
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = CumulativeExecutionTime.GetHashCode();
            hashCode = (hashCode * 397) ^ TotalMemoryBytes.GetHashCode();
            hashCode = (hashCode * 397) ^ ExecutionsCount.GetHashCode();
            return hashCode;
        }
    }
}
