// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Identifies a distinct project evaluation for snapshot lookup.
/// </summary>
internal sealed class ProjectInstanceSnapshotCacheKey : IEquatable<ProjectInstanceSnapshotCacheKey>
{
    private readonly string _projectFullPath;
    private readonly string _toolsVersion;
    private readonly bool _explicitToolsVersionSpecified;
    private readonly string? _subToolsetVersion;
    private readonly ProjectLoadSettings _projectLoadSettings;
    private readonly PropertyDictionary<ProjectPropertyInstance> _globalProperties;
    private readonly int _globalPropertiesHashCode;

    internal ProjectInstanceSnapshotCacheKey(
        string projectFullPath,
        string toolsVersion,
        bool explicitToolsVersionSpecified,
        string? subToolsetVersion,
        ProjectLoadSettings projectLoadSettings,
        IReadOnlyDictionary<string, string> globalProperties)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFullPath);
        ArgumentException.ThrowIfNullOrEmpty(toolsVersion);
        ArgumentNullException.ThrowIfNull(globalProperties);

        _projectFullPath = FileUtilities.NormalizePath(projectFullPath);
        _toolsVersion = toolsVersion;
        _explicitToolsVersionSpecified = explicitToolsVersionSpecified;
        _subToolsetVersion = subToolsetVersion;
        _projectLoadSettings = projectLoadSettings;
        _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(globalProperties.Count);

        foreach (KeyValuePair<string, string> property in globalProperties)
        {
            _globalProperties[property.Key] = ProjectPropertyInstance.Create(property.Key, property.Value);
        }

        int globalPropertiesHashCode = _globalProperties.Count;
        foreach (ProjectPropertyInstance property in _globalProperties)
        {
            int propertyHashCode = MSBuildNameIgnoreCaseComparer.Default.GetHashCode(property.Name);
            propertyHashCode =
                (propertyHashCode * 397) ^
                StringComparer.Ordinal.GetHashCode(((IProperty)property).EvaluatedValueEscaped);
            globalPropertiesHashCode += MixHashCode(propertyHashCode);
        }

        _globalPropertiesHashCode = MixHashCode(globalPropertiesHashCode);
    }

    public bool Equals(ProjectInstanceSnapshotCacheKey? other)
    {
        return other is not null &&
            FileUtilities.PathComparer.Equals(_projectFullPath, other._projectFullPath) &&
            _toolsVersion.Equals(other._toolsVersion, StringComparison.OrdinalIgnoreCase) &&
            _explicitToolsVersionSpecified == other._explicitToolsVersionSpecified &&
            StringComparer.OrdinalIgnoreCase.Equals(_subToolsetVersion, other._subToolsetVersion) &&
            _projectLoadSettings == other._projectLoadSettings &&
            _globalProperties.Equals(other._globalProperties);
    }

    public override bool Equals(object? obj) =>
        obj is ProjectInstanceSnapshotCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        int hashCode = FileUtilities.PathComparer.GetHashCode(_projectFullPath);
        hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(_toolsVersion);
        hashCode = (hashCode * 397) ^ _explicitToolsVersionSpecified.GetHashCode();
        hashCode = (hashCode * 397) ^
            (_subToolsetVersion is null
                ? 0
                : StringComparer.OrdinalIgnoreCase.GetHashCode(_subToolsetVersion));
        hashCode = (hashCode * 397) ^ (int)_projectLoadSettings;
        hashCode = (hashCode * 397) ^ _globalPropertiesHashCode;
        return hashCode;
    }

    private static int MixHashCode(int hashCode)
    {
        unchecked
        {
            hashCode ^= (int)((uint)hashCode >> 16);
            hashCode *= 0x45D9F3B;
            hashCode ^= (int)((uint)hashCode >> 16);
            hashCode *= 0x45D9F3B;
            hashCode ^= (int)((uint)hashCode >> 16);
            return hashCode;
        }
    }
}
