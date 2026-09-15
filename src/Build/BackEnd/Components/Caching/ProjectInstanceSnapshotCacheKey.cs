// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Identifies project evaluation request configuration for snapshot lookup.
/// </summary>
/// <remarks>
/// Key equality does not establish that a snapshot may be reused. Project and import contents,
/// file enumeration, relevant environment values, and SDK and toolset resolution state must be
/// verified separately by <see cref="IProjectInstanceSnapshotValidator"/>.
/// </remarks>
internal sealed class ProjectInstanceSnapshotCacheKey : IEquatable<ProjectInstanceSnapshotCacheKey>
{
    private readonly string _projectFullPath;
    private readonly string _toolsVersion;
    private readonly bool _explicitToolsVersionSpecified;
    private readonly string? _subToolsetVersion;
    private readonly ProjectLoadSettings _projectLoadSettings;
    private readonly bool _interactive;
    private readonly int _maxNodeCount;
    private readonly string _startupDirectory;
    private readonly string _workingDirectory;
    private readonly string _culture;
    private readonly string _uiCulture;
    private readonly string _engineVersion;
    private readonly string? _disabledChangeWave;
    private readonly long _environmentFingerprint;
    private readonly long _parserConfigurationFingerprint;
    private readonly long _toolsetFingerprint;
    private readonly string _toolsPath;
    private readonly string _commandLinePropertyNames;
    private readonly PropertyDictionary<ProjectPropertyInstance> _globalProperties;
    private readonly string _formattedGlobalProperties;
    private readonly int _globalPropertiesHashCode;

    internal ProjectInstanceSnapshotCacheKey(
        string projectFullPath,
        string toolsVersion,
        bool explicitToolsVersionSpecified,
        string? subToolsetVersion,
        ProjectLoadSettings projectLoadSettings,
        IReadOnlyDictionary<string, string> globalProperties,
        bool interactive = false,
        int maxNodeCount = 0,
        string? startupDirectory = null,
        string? workingDirectory = null,
        string? culture = null,
        string? uiCulture = null,
        string? engineVersion = null,
        string? disabledChangeWave = null,
        long environmentFingerprint = 0,
        long parserConfigurationFingerprint = 0,
        long toolsetFingerprint = 0,
        string? toolsPath = null,
        string? commandLinePropertyNames = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFullPath);
        ArgumentException.ThrowIfNullOrEmpty(toolsVersion);
        ArgumentNullException.ThrowIfNull(globalProperties);

        _projectFullPath = FileUtilities.NormalizePath(projectFullPath);
        _toolsVersion = toolsVersion;
        _explicitToolsVersionSpecified = explicitToolsVersionSpecified;
        _subToolsetVersion = subToolsetVersion;
        _projectLoadSettings = projectLoadSettings;
        _interactive = interactive;
        _maxNodeCount = maxNodeCount;
        _startupDirectory = startupDirectory ?? string.Empty;
        _workingDirectory = workingDirectory ?? string.Empty;
        _culture = culture ?? string.Empty;
        _uiCulture = uiCulture ?? string.Empty;
        _engineVersion = engineVersion ?? string.Empty;
        _disabledChangeWave = disabledChangeWave;
        _environmentFingerprint = environmentFingerprint;
        _parserConfigurationFingerprint = parserConfigurationFingerprint;
        _toolsetFingerprint = toolsetFingerprint;
        _toolsPath = toolsPath ?? string.Empty;
        _commandLinePropertyNames = commandLinePropertyNames ?? string.Empty;
        _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(globalProperties.Count);

        foreach (KeyValuePair<string, string> property in globalProperties)
        {
            _globalProperties[property.Key] = ProjectPropertyInstance.Create(property.Key, property.Value);
        }

        _formattedGlobalProperties = FormatGlobalProperties(_globalProperties);

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
            _interactive == other._interactive &&
            _maxNodeCount == other._maxNodeCount &&
            FileUtilities.PathComparer.Equals(_startupDirectory, other._startupDirectory) &&
            FileUtilities.PathComparer.Equals(_workingDirectory, other._workingDirectory) &&
            _culture.Equals(other._culture, StringComparison.Ordinal) &&
            _uiCulture.Equals(other._uiCulture, StringComparison.Ordinal) &&
            _engineVersion.Equals(other._engineVersion, StringComparison.Ordinal) &&
            StringComparer.Ordinal.Equals(_disabledChangeWave, other._disabledChangeWave) &&
            _environmentFingerprint == other._environmentFingerprint &&
            _parserConfigurationFingerprint == other._parserConfigurationFingerprint &&
            _toolsetFingerprint == other._toolsetFingerprint &&
            FileUtilities.PathComparer.Equals(_toolsPath, other._toolsPath) &&
            _commandLinePropertyNames.Equals(other._commandLinePropertyNames, StringComparison.Ordinal) &&
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
        hashCode = (hashCode * 397) ^ _interactive.GetHashCode();
        hashCode = (hashCode * 397) ^ _maxNodeCount;
        hashCode = (hashCode * 397) ^ FileUtilities.PathComparer.GetHashCode(_startupDirectory);
        hashCode = (hashCode * 397) ^ FileUtilities.PathComparer.GetHashCode(_workingDirectory);
        hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_culture);
        hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_uiCulture);
        hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_engineVersion);
        hashCode = (hashCode * 397) ^
            (_disabledChangeWave is null ? 0 : StringComparer.Ordinal.GetHashCode(_disabledChangeWave));
        hashCode = (hashCode * 397) ^ _environmentFingerprint.GetHashCode();
        hashCode = (hashCode * 397) ^ _parserConfigurationFingerprint.GetHashCode();
        hashCode = (hashCode * 397) ^ _toolsetFingerprint.GetHashCode();
        hashCode = (hashCode * 397) ^ FileUtilities.PathComparer.GetHashCode(_toolsPath);
        hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_commandLinePropertyNames);
        hashCode = (hashCode * 397) ^ _globalPropertiesHashCode;
        return hashCode;
    }

    internal bool Matches(EvaluationInputKey key) => GetMismatch(key) is null;

    internal string? GetMismatch(EvaluationInputKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!FileUtilities.PathComparer.Equals(_projectFullPath, key.ProjectFullPath))
        {
            return nameof(key.ProjectFullPath);
        }
        if (!_formattedGlobalProperties.Equals(key.GlobalProperties, StringComparison.Ordinal))
        {
            return nameof(key.GlobalProperties);
        }
        if (!_toolsVersion.Equals(key.ToolsVersion, StringComparison.OrdinalIgnoreCase))
        {
            return nameof(key.ToolsVersion);
        }
        if (_explicitToolsVersionSpecified != key.ExplicitToolsVersionSpecified)
        {
            return nameof(key.ExplicitToolsVersionSpecified);
        }
        if (!_commandLinePropertyNames.Equals(key.CommandLinePropertyNames, StringComparison.Ordinal))
        {
            return nameof(key.CommandLinePropertyNames);
        }
        if (!StringComparer.OrdinalIgnoreCase.Equals(_subToolsetVersion, key.SubToolsetVersion))
        {
            return nameof(key.SubToolsetVersion);
        }
        if (_projectLoadSettings != key.LoadSettings)
        {
            return nameof(key.LoadSettings);
        }
        if (_interactive != key.Interactive)
        {
            return nameof(key.Interactive);
        }
        if (_maxNodeCount != key.MaxNodeCount)
        {
            return nameof(key.MaxNodeCount);
        }
        if (!FileUtilities.PathComparer.Equals(_startupDirectory, key.StartupDirectory))
        {
            return nameof(key.StartupDirectory);
        }
        if (!FileUtilities.PathComparer.Equals(_workingDirectory, key.WorkingDirectory))
        {
            return nameof(key.WorkingDirectory);
        }
        if (!_culture.Equals(key.Culture, StringComparison.Ordinal))
        {
            return nameof(key.Culture);
        }
        if (!_uiCulture.Equals(key.UICulture, StringComparison.Ordinal))
        {
            return nameof(key.UICulture);
        }
        if (!_engineVersion.Equals(key.EngineVersion, StringComparison.Ordinal))
        {
            return nameof(key.EngineVersion);
        }
        if (!StringComparer.Ordinal.Equals(_disabledChangeWave, key.DisabledChangeWave))
        {
            return nameof(key.DisabledChangeWave);
        }
        if (_environmentFingerprint != key.EnvironmentFingerprint)
        {
            return nameof(key.EnvironmentFingerprint);
        }
        if (_parserConfigurationFingerprint != key.ParserConfigurationFingerprint)
        {
            return nameof(key.ParserConfigurationFingerprint);
        }
        if (_toolsetFingerprint != key.ToolsetFingerprint)
        {
            return nameof(key.ToolsetFingerprint);
        }
        if (!FileUtilities.PathComparer.Equals(_toolsPath, key.ToolsPath))
        {
            return nameof(key.ToolsPath);
        }
        return null;
    }

    internal EvaluationInputKey ToEvaluationInputKey() =>
        new(
            _projectFullPath,
            _formattedGlobalProperties,
            _toolsVersion,
            _explicitToolsVersionSpecified,
            _commandLinePropertyNames,
            _toolsPath,
            _subToolsetVersion,
            _toolsetFingerprint,
            ProjectEvaluationStage.Full,
            _projectLoadSettings,
            _interactive,
            _maxNodeCount,
            _startupDirectory,
            _workingDirectory,
            _culture,
            _uiCulture,
            _engineVersion,
            _disabledChangeWave,
            _environmentFingerprint,
            _parserConfigurationFingerprint);

    private static string FormatGlobalProperties(
        IEnumerable<ProjectPropertyInstance> properties)
    {
        ProjectPropertyInstance[] ordered = properties
            .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var builder = new System.Text.StringBuilder();
        foreach (ProjectPropertyInstance property in ordered)
        {
            builder.Append(property.Name.ToUpperInvariant())
                .Append('=')
                .Append(((IProperty)property).EvaluatedValueEscaped)
                .Append('\0');
        }

        return builder.ToString();
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
