// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Represents a unique key for task or target telemetry data.
/// </summary>
/// <remarks>
/// Used as a dictionary key for tracking execution metrics of tasks and targets.
/// </remarks>
internal struct TaskOrTargetTelemetryKey : IEquatable<TaskOrTargetTelemetryKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskOrTargetTelemetryKey"/> struct with all properties.
    /// </summary>
    /// <param name="name">The name of the task or target.</param>
    /// <param name="isCustom">Indicates whether the task/target is custom.</param>
    /// <param name="isFromNugetCache">Indicates whether the task/target is from NuGet cache.</param>
    /// <param name="isFromMetaProject">Indicates whether the task/target is from a meta project.</param>
    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache, bool isFromMetaProject)
    {
        Name = name;
        IsCustom = isCustom;
        IsNuget = isFromNugetCache;
        IsMetaProj = isFromMetaProject;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskOrTargetTelemetryKey"/> struct without meta project flag.
    /// </summary>
    /// <param name="name">The name of the task or target.</param>
    /// <param name="isCustom">Indicates whether the task/target is custom.</param>
    /// <param name="isFromNugetCache">Indicates whether the task/target is from NuGet cache.</param>
    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache)
    {
        Name = name;
        IsCustom = isCustom;
        IsNuget = isFromNugetCache;
        IsMetaProj = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskOrTargetTelemetryKey"/> struct with name only.
    /// </summary>
    /// <param name="name">The name of the task or target.</param>
    public TaskOrTargetTelemetryKey(string name) : this(name, false, false, false) { }

    /// <summary>
    /// Enables explicit casting from string to <see cref="TaskOrTargetTelemetryKey"/>.
    /// </summary>
    /// <param name="key">The string name to convert to a telemetry key.</param>
    /// <returns>A telemetry key with the given name.</returns>
    public static explicit operator TaskOrTargetTelemetryKey(string key) => new(key);

    /// <summary>
    /// Gets the name of the task or target.
    /// </summary>
    /// <remarks>
    /// This name is used as the primary key in serialized JSON data.
    /// It is hashed when the task/target is custom or from a meta project.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Indicates whether the task/target is custom.
    /// </summary>
    public bool IsCustom { get; }

    /// <summary>
    /// Indicates whether the task/target is from NuGet cache.
    /// </summary>
    /// <remarks>Those can be custom or MSFT provided ones.</remarks>
    public bool IsNuget { get; }

    /// <summary>
    /// Indicates whether the task/target is generated during build from a metaproject.
    /// </summary>
    /// <remarks>Those must be hashed (as they contain paths).</remarks>
    public bool IsMetaProj { get; }

    public override bool Equals(object? obj)
    {
        if (obj is TaskOrTargetTelemetryKey other)
        {
            return Equals(other);
        }
        return false;
    }

    public bool Equals(TaskOrTargetTelemetryKey other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
           IsCustom == other.IsCustom &&
           IsNuget == other.IsNuget &&
           IsMetaProj == other.IsMetaProj;

    // We need hash code and equals - so that we can stuff data into dictionaries
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ IsCustom.GetHashCode();
            hashCode = (hashCode * 397) ^ IsNuget.GetHashCode();
            hashCode = (hashCode * 397) ^ IsMetaProj.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString() => $"{Name},Custom:{IsCustom},IsFromNugetCache:{IsNuget},IsFromMetaProject:{IsMetaProj}";
}
