// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

internal struct TaskOrTargetTelemetryKey : IEquatable<TaskOrTargetTelemetryKey>
{
    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache, bool isFromMetaProject)
    {
        Name = name;
        IsCustom = isCustom;
        IsFromNugetCache = isFromNugetCache;
        IsFromMetaProject = isFromMetaProject;
    }

    public TaskOrTargetTelemetryKey(string name, bool isCustom, bool isFromNugetCache)
    {
        Name = name;
        IsCustom = isCustom;
        IsFromNugetCache = isFromNugetCache;
    }

    public TaskOrTargetTelemetryKey(string name) => Name = name;

    public static explicit operator TaskOrTargetTelemetryKey(string key) => new(key);

    public string Name { get; }
    // Indicate custom targets/task - those must be hashed.
    public bool IsCustom { get; }
    // Indicate targets/tasks sourced from nuget cache - those can be custom or MSFT provided ones.
    public bool IsFromNugetCache { get; }
    // Indicate targets/tasks generated during build - those must be hashed (as they contain paths).
    public bool IsFromMetaProject { get; }

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
           IsFromNugetCache == other.IsFromNugetCache &&
           IsFromMetaProject == other.IsFromMetaProject;

    // We need hash code and equals - so that we can stuff data into dictionaries
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ IsCustom.GetHashCode();
            hashCode = (hashCode * 397) ^ IsFromNugetCache.GetHashCode();
            hashCode = (hashCode * 397) ^ IsFromMetaProject.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString() => $"{Name},Custom:{IsCustom},IsFromNugetCache:{IsFromNugetCache},IsFromMetaProject:{IsFromMetaProject}";
}
