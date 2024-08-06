﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck.Utilities;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Configuration for a build check.
/// Default values can be specified by the Check in code.
/// Users can overwrite the defaults by explicit settings in the .editorconfig file.
/// Each rule can have its own configuration, which can differ per each project.
/// The <see cref="EvaluationCheckScope"/> setting must be same for all rules in the same check (but can differ between projects)
/// </summary>
public class BuildExecutionCheckConfiguration
{
    // Defaults to be used if any configuration property is not specified neither as default
    //  nor in the editorconfig configuration file.
    public static BuildExecutionCheckConfiguration Default { get; } = new()
    {
        EvaluationCheckScope = BuildCheck.EvaluationCheckScope.ProjectOnly,
        Severity = BuildExecutionCheckResultSeverity.None
    };

    public static BuildExecutionCheckConfiguration Null { get; } = new();

    public string? RuleId { get; internal set; }

    /// <summary>
    /// This applies only to specific events, that can distinguish whether they are directly inferred from
    ///  the current project, or from some import. If supported it can help tuning the level of detail or noise from check.
    ///
    /// If not supported by the data source - then the setting is ignored
    /// </summary>
    public EvaluationCheckScope? EvaluationCheckScope { get; internal init; }

    /// <summary>
    /// The severity of the result for the rule.
    /// </summary>
    public BuildExecutionCheckResultSeverity? Severity { get; internal init; }

    /// <summary>
    /// Whether the check rule is enabled.
    /// If all rules within the check are not enabled, it will not be run.
    /// If some rules are enabled and some are not, the check will be run and reports will be post-filtered.
    /// </summary>
    public bool? IsEnabled {
        get
        {
            // Do not consider Default as enabled, because the default severity of the rule could be set to None
            if (Severity.HasValue && Severity.Value != BuildExecutionCheckResultSeverity.Default)
            {
                return !Severity.Value.Equals(BuildExecutionCheckResultSeverity.None);
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a <see cref="BuildExecutionCheckConfiguration"/> object based on the provided configuration dictionary.
    /// If the BuildCheckConfiguration's property name presented in the dictionary, the value of this key-value pair is parsed and assigned to the instance's field.
    /// If parsing failed the value will be equal to null.
    /// </summary>
    /// <param name="configDictionary">The configuration dictionary containing the settings for the build check. The configuration's keys are expected to be in lower case or the EqualityComparer to ignore case.</param>
    /// <returns>A new instance of <see cref="BuildExecutionCheckConfiguration"/> with the specified settings.</returns>
    internal static BuildExecutionCheckConfiguration Create(Dictionary<string, string>? configDictionary) => new()
    {
        EvaluationCheckScope = TryExtractEvaluationCheckScope(configDictionary),
        Severity = TryExtractSeverity(configDictionary),
    };


    private static EvaluationCheckScope? TryExtractEvaluationCheckScope(Dictionary<string, string>? config)
    {

        if (!TryExtractValue(BuildCheckConstants.scopeConfigurationKey, config, out string? stringValue) || stringValue is null)
        {
            return null;
        }

        switch (stringValue)
        {
            case "project":
                return BuildCheck.EvaluationCheckScope.ProjectOnly;
            case "current_imports":
                return BuildCheck.EvaluationCheckScope.ProjectWithImportsFromCurrentWorkTree;
            case "without_sdks":
                return BuildCheck.EvaluationCheckScope.ProjectWithImportsWithoutSdks;
            case "all":
                return BuildCheck.EvaluationCheckScope.ProjectWithAllImports;
            default:
                ThrowIncorrectValueException(BuildCheckConstants.scopeConfigurationKey, stringValue);
                break;
        }

        return null;
    }

    private static BuildExecutionCheckResultSeverity? TryExtractSeverity(Dictionary<string, string>? config)
    {
        if (!TryExtractValue(BuildCheckConstants.severityConfigurationKey, config, out string? stringValue) || stringValue is null)
        {
            return null;
        }

        switch (stringValue)
        {
            case "none":
                return BuildExecutionCheckResultSeverity.None;
            case "default":
                return BuildExecutionCheckResultSeverity.Default;
            case "suggestion":
                return BuildExecutionCheckResultSeverity.Suggestion;
            case "warning":
                return BuildExecutionCheckResultSeverity.Warning;
            case "error":
                return BuildExecutionCheckResultSeverity.Error;
            default:
                ThrowIncorrectValueException(BuildCheckConstants.severityConfigurationKey, stringValue);
                break;
        }

        return null;
    }

    private static bool TryExtractValue(string key, Dictionary<string, string>? config, out string? stringValue)
    {
        stringValue = null;

        if (config == null || !config.TryGetValue(key.ToLower(), out stringValue) || stringValue is null)
        {
            return false;
        }

        stringValue = stringValue.ToLower();

        return true;
    }

    private static void ThrowIncorrectValueException(string key, string value)
    {
        // TODO: It will be nice to have the filename where the incorrect configuration was placed. 
        throw new BuildCheckConfigurationException(
                $"Incorrect value provided in config for key {key}: '{value}'",
                buildCheckConfigurationErrorScope: BuildCheckConfigurationErrorScope.EditorConfigParser);
    }
}
