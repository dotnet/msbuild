// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry;

internal struct TaskOrTargetTelemetryKey : IEquatable<TaskOrTargetTelemetryKey>
{
    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache, bool isFromMetaProject)
    {
        Name = name;
        IsCustom = isCustom;
        IsNuget = isFromNugetCache;
        IsMetaProj = isFromMetaProject;
    }

    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache)
    {
        Name = name;
        IsCustom = isCustom;
        IsNuget = isFromNugetCache;
    }

    public TaskOrTargetTelemetryKey(string name) => Name = name;

    public static explicit operator TaskOrTargetTelemetryKey(string key) => new(key);

    public string Name { get; }

    /// <summary>
    /// Indicate custom targets/task - those must be hashed.
    /// </summary>
    public bool IsCustom { get; }

    /// <summary>
    /// Indicate targets/tasks sourced from NuGet cache - those can be custom or MSFT provided ones.
    /// </summary>
    public bool IsNuget { get; }

    /// <summary>
    /// Indicate targets/tasks generated during build - those must be hashed (as they contain paths).
    /// </summary>
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
