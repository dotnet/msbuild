// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Analyzers;

internal class PropertiesUsageAnalyzer : InternalBuildAnalyzer
{
    private static readonly BuildAnalyzerRule _usedBeforeInitializedRule = new BuildAnalyzerRule("BC0201", "PropertyUsedBeforeDeclared",
        "A property that is accessed should be declared first.",
        "Property: [{0}] was accessed, but it was never initialized.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, EvaluationAnalysisScope = EvaluationAnalysisScope.ProjectOnly });

    private static readonly BuildAnalyzerRule _initializedAfterUsedRule = new BuildAnalyzerRule("BC0202", "PropertyDeclaredAfterUsed",
        "A property should be declared before it is first used.",
        "Property: [{0}] first declared/initialized at [{1}] used before it was initialized.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, EvaluationAnalysisScope = EvaluationAnalysisScope.ProjectOnly });

    private static readonly BuildAnalyzerRule _unusedPropertyRule = new BuildAnalyzerRule("BC0203", "UnusedPropertyDeclared",
        "A property that is not used should not be declared.",
        "Property: [{0}] was declared/initialized, but it was never used.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, EvaluationAnalysisScope = EvaluationAnalysisScope.ProjectOnly });

    internal static readonly IReadOnlyList<BuildAnalyzerRule> SupportedRulesList = [_usedBeforeInitializedRule, _initializedAfterUsedRule, _unusedPropertyRule];

    public override string FriendlyName => "MSBuild.PropertiesUsageAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules => SupportedRulesList;

    private const string _allowUninitPropsInConditionsKey = "AllowUninitializedPropertiesInConditions";
    private bool _allowUninitPropsInConditions = false;
    // TODO: Add scope to configuration visible by the analyzer - and reflect on it
    public override void Initialize(ConfigurationContext configurationContext)
    {
        bool? allowUninitPropsInConditionsRule1 = null;
        bool? allowUninitPropsInConditionsRule2 = null;

        foreach (CustomConfigurationData customConfigurationData in configurationContext.CustomConfigurationData)
        {
            allowUninitPropsInConditionsRule1 =
                GetAllowUninitPropsInConditionsConfig(customConfigurationData, _usedBeforeInitializedRule.Id);
            allowUninitPropsInConditionsRule2 =
                GetAllowUninitPropsInConditionsConfig(customConfigurationData, _initializedAfterUsedRule.Id);
        }

        if (allowUninitPropsInConditionsRule1.HasValue &&
            allowUninitPropsInConditionsRule2.HasValue &&
            allowUninitPropsInConditionsRule1 != allowUninitPropsInConditionsRule2)
        {
            throw new BuildCheckConfigurationException(
                $"[{_usedBeforeInitializedRule.Id}] and [{_initializedAfterUsedRule.Id}] are not allowed to have differing configuration value for [{_allowUninitPropsInConditionsKey}]");
        }

        if (allowUninitPropsInConditionsRule1.HasValue || allowUninitPropsInConditionsRule2.HasValue)
        {
            _allowUninitPropsInConditions = allowUninitPropsInConditionsRule1 ?? allowUninitPropsInConditionsRule2 ?? false;
        }
    }

    private static bool? GetAllowUninitPropsInConditionsConfig(CustomConfigurationData customConfigurationData,
        string ruleId)
    {
        if (customConfigurationData.RuleId.Equals(ruleId, StringComparison.InvariantCultureIgnoreCase) &&
            (customConfigurationData.ConfigurationData?.TryGetValue(_allowUninitPropsInConditionsKey, out string? configVal) ?? false))
        {
            return bool.Parse(configVal);
        }

        return null;
    }

    public override void RegisterInternalActions(IInternalBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterPropertyReadAction(ProcessPropertyRead);
        registrationContext.RegisterPropertyWriteAction(ProcessPropertyWrite);
        registrationContext.RegisterProjectProcessingDoneAction(DoneWithProject);
    }

    private Dictionary<string, IMsBuildElementLocation?> _writenProperties = new(MSBuildNameIgnoreCaseComparer.Default);
    private HashSet<string> _readProperties = new(MSBuildNameIgnoreCaseComparer.Default);
    private Dictionary<string, IMsBuildElementLocation> _uninitializedReads = new(MSBuildNameIgnoreCaseComparer.Default);

    // TODO: this is temporary - will be improved once we have scoping argument propagated to user config data.
    private bool IsActionInObservedScope(IMsBuildElementLocation? location, string projectFilePath)
    {
        return location != null && location.File == projectFilePath;
    }

    private void ProcessPropertyWrite(BuildCheckDataContext<PropertyWriteData> context)
    {
        PropertyWriteData writeData = context.Data;

        if (IsActionInObservedScope(writeData.ElementLocation, writeData.ProjectFilePath))
        {
            _writenProperties[writeData.PropertyName] = writeData.ElementLocation;
        }

        if (!writeData.IsEmpty &&
            _uninitializedReads.TryGetValue(writeData.PropertyName, out IMsBuildElementLocation? uninitReadLocation))
        {
            _uninitializedReads.Remove(writeData.PropertyName);

            context.ReportResult(BuildCheckResult.Create(
                _initializedAfterUsedRule,
                uninitReadLocation,
                writeData.PropertyName, writeData.ElementLocation?.LocationString ?? string.Empty));
        }
    }

    private void ProcessPropertyRead(BuildCheckDataContext<PropertyReadData> context)
    {
        PropertyReadData readData = context.Data;

        if (readData.PropertyReadContext != PropertyReadContext.PropertyEvaluationSelf)
        {
            _readProperties.Add(readData.PropertyName);
        }

        if (readData.IsUninitialized &&
            readData.PropertyReadContext != PropertyReadContext.PropertyEvaluationSelf &&
            readData.PropertyReadContext != PropertyReadContext.ConditionEvaluationWithOneSideEmpty &&
            (!_allowUninitPropsInConditions ||
             readData.PropertyReadContext != PropertyReadContext.ConditionEvaluation) &&
            IsActionInObservedScope(readData.ElementLocation, readData.ProjectFilePath))
        {
            _uninitializedReads[readData.PropertyName] = readData.ElementLocation;
        }
    }

    private void DoneWithProject(BuildCheckDataContext<ProjectProcessingDoneData> context)
    {
        foreach (var propWithLocation in _writenProperties)
        {
            if (propWithLocation.Value != null && !_readProperties.Contains(propWithLocation.Key))
            {
                context.ReportResult(BuildCheckResult.Create(
                    _unusedPropertyRule,
                    propWithLocation.Value,
                    propWithLocation.Key));
            }
        }

        foreach (var uninitializedRead in _uninitializedReads)
        {
            context.ReportResult(BuildCheckResult.Create(
                _usedBeforeInitializedRule,
                uninitializedRead.Value,
                uninitializedRead.Key));
        }

        _readProperties = new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
        _writenProperties = new Dictionary<string, IMsBuildElementLocation?>(MSBuildNameIgnoreCaseComparer.Default);
        _uninitializedReads = new Dictionary<string, IMsBuildElementLocation>(MSBuildNameIgnoreCaseComparer.Default);
    }
}
