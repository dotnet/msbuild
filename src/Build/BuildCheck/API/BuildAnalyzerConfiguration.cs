// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Configuration for a build analyzer.
/// Default values can be specified by the Analyzer in code.
/// Users can overwrite the defaults by explicit settings in the .editorconfig file.
/// Each rule can have its own configuration, which can differ per each project.
/// The <see cref="EvaluationAnalysisScope"/> setting must be same for all rules in the same analyzer (but can differ between projects)
/// </summary>
public class BuildAnalyzerConfiguration
{
    // Defaults to be used if any configuration property is not specified neither as default
    //  nor in the editorconfig configuration file.
    public static BuildAnalyzerConfiguration Default { get; } = new()
    {
        EvaluationAnalysisScope = BuildCheck.EvaluationAnalysisScope.ProjectOnly,
        Severity = BuildAnalyzerResultSeverity.Info,
        IsEnabled = false,
    };

    public static BuildAnalyzerConfiguration Null { get; } = new();

    /// <summary>
    /// This applies only to specific events, that can distinguish whether they are directly inferred from
    ///  the current project, or from some import. If supported it can help tuning the level of detail or noise from analysis.
    ///
    /// If not supported by the data source - then the setting is ignored
    /// </summary>
    public EvaluationAnalysisScope? EvaluationAnalysisScope { get; internal init; }

    /// <summary>
    /// The severity of the result for the rule.
    /// </summary>
    public BuildAnalyzerResultSeverity? Severity { get; internal init; }

    /// <summary>
    /// Whether the analyzer rule is enabled.
    /// If all rules within the analyzer are not enabled, it will not be run.
    /// If some rules are enabled and some are not, the analyzer will be run and reports will be post-filtered.
    /// </summary>
    public bool? IsEnabled { get; internal init; }

    /// <summary>
    /// Creates a <see cref="BuildAnalyzerConfiguration"/> object based on the provided configuration dictionary.
    /// If key, equals to the name of the property in lowercase, exists in the dictionary => the value is parsed and assigned to the instance property value.
    /// </summary>
    /// <param name="configDictionary">The configuration dictionary containing the settings for the build analyzer.</param>
    /// <returns>A new instance of <see cref="BuildAnalyzerConfiguration"/> with the specified settings.</returns>
    public static BuildAnalyzerConfiguration Create(Dictionary<string, string>? configDictionary)
    {
        return new()
        {
            EvaluationAnalysisScope = TryExtractValue(nameof(EvaluationAnalysisScope).ToLower(), configDictionary, out EvaluationAnalysisScope evaluationAnalysisScope) ? evaluationAnalysisScope : null,
            Severity = TryExtractValue(nameof(Severity).ToLower(), configDictionary, out BuildAnalyzerResultSeverity severity) ? severity : null,
            IsEnabled = TryExtractValue(nameof(IsEnabled).ToLower(), configDictionary, out bool isEnabled) ? isEnabled : null,
        };
    }

    private static bool TryExtractValue<T>(string key, Dictionary<string, string>? config, out T value) where T : struct, Enum
    {
        value = default;

        if (config == null || !config.TryGetValue(key, out var stringValue) || stringValue is null)
        {
            return false;
        }

        var isParsed = Enum.TryParse(stringValue, true, out value);

        if (!isParsed)
        {
            ThrowIncorectValueEception(key, stringValue);
        }

        return isParsed;
    }

    private static bool TryExtractValue(string key, Dictionary<string, string>? config, out bool value)
    {
        value = default;

        if (config == null || !config.TryGetValue(key, out var stringValue) || stringValue is null)
        {
            return false;
        }

        bool isParsed = false;
        
        if (bool.TryParse(stringValue, out bool boolValue))
        {
            value = boolValue;
            isParsed = true;
        }
        
        if (!isParsed)
        {
            ThrowIncorectValueEception(key, stringValue);
        }

        return isParsed;
    }

    private static void ThrowIncorectValueEception(string key, string value)
    {
        throw new BuildCheckConfigurationException(
                $"Incorrect value provided in config for key {key}: '{value}'",
                buildCheckConfigurationErrorScope: BuildCheckConfigurationErrorScope.EditorConfigParser);
    }
}
