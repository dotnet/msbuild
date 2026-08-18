// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Represents the execution statistics of tasks executed on a node.
/// </summary>
internal class TaskExecutionStats(
    TimeSpan cumulativeExecutionTime,
    int executionsCount,
    long totalMemoryConsumption,
    string? taskFactoryName,
    string? taskHostRuntime)
{
    private TaskExecutionStats()
        : this(TimeSpan.Zero, 0, 0, null, null)
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
    /// The name of the task factory used to create this task.
    /// Examples: AssemblyTaskFactory, IntrinsicTaskFactory, CodeTaskFactory, 
    /// RoslynCodeTaskFactory, XamlTaskFactory, or a custom factory name.
    /// </summary>
    public string? TaskFactoryName { get; set; } = taskFactoryName;

    /// <summary>
    /// The runtime specified for out-of-process task execution.
    /// Values: "CLR2", "CLR4", "NET", or null if not specified.
    /// </summary>
    public string? TaskHostRuntime { get; set; } = taskHostRuntime;

    /// <summary>
    /// Accumulates statistics from another instance into this one.
    /// </summary>
    /// <param name="other">Statistics to add to this instance.</param>
    internal void Accumulate(TaskExecutionStats other)
    {
        CumulativeExecutionTime += other.CumulativeExecutionTime;
        TotalMemoryBytes += other.TotalMemoryBytes;
        ExecutionsCount += other.ExecutionsCount;
        TaskFactoryName ??= other.TaskFactoryName;
        TaskHostRuntime ??= other.TaskHostRuntime;
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
           ExecutionsCount == other.ExecutionsCount &&
           TaskFactoryName == other.TaskFactoryName &&
           TaskHostRuntime == other.TaskHostRuntime;

    // Needed since we override Equals
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = CumulativeExecutionTime.GetHashCode();
            hashCode = (hashCode * 397) ^ TotalMemoryBytes.GetHashCode();
            hashCode = (hashCode * 397) ^ ExecutionsCount.GetHashCode();
            hashCode = (hashCode * 397) ^ (TaskFactoryName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (TaskHostRuntime?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}
