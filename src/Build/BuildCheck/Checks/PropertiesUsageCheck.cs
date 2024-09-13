// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal class PropertiesUsageCheck : InternalCheck
{
    private static readonly CheckRule _usedBeforeInitializedRule = new CheckRule("BC0201", "PropertyUsedBeforeDeclared",
        ResourceUtilities.GetResourceString("BuildCheck_BC0201_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0201_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Warning, EvaluationCheckScope = EvaluationCheckScope.ProjectFileOnly });

    private static readonly CheckRule _initializedAfterUsedRule = new CheckRule("BC0202", "PropertyDeclaredAfterUsed",
        ResourceUtilities.GetResourceString("BuildCheck_BC0202_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0202_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Warning, EvaluationCheckScope = EvaluationCheckScope.ProjectFileOnly });

    private static readonly CheckRule _unusedPropertyRule = new CheckRule("BC0203", "UnusedPropertyDeclared",
        ResourceUtilities.GetResourceString("BuildCheck_BC0203_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0203_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Suggestion, EvaluationCheckScope = EvaluationCheckScope.ProjectFileOnly });

    internal static readonly IReadOnlyList<CheckRule> SupportedRulesList = [_usedBeforeInitializedRule, _initializedAfterUsedRule, _unusedPropertyRule];

    public override string FriendlyName => "MSBuild.PropertiesUsageAnalyzer";

    public override IReadOnlyList<CheckRule> SupportedRules => SupportedRulesList;

    private const string _allowUninitPropsInConditionsKey = "AllowUninitializedPropertiesInConditions";
    private bool _allowUninitPropsInConditions = false;
    // Each check can have it's scope and enablement
    private EvaluationCheckScope _uninitializedReadScope;
    private EvaluationCheckScope _unusedPropertyScope;
    private EvaluationCheckScope _initializedAfterUseScope;
    private bool _uninitializedReadEnabled;
    private bool _unusedPropertyEnabled;
    private bool _initializedAfterUseEnabled;
    public override void Initialize(ConfigurationContext configurationContext)
    {
        var config = configurationContext.CheckConfig.FirstOrDefault(c => c.RuleId == _usedBeforeInitializedRule.Id)
                ?? CheckConfigurationEffective.Default;

        _uninitializedReadEnabled = config.IsEnabled;
        _uninitializedReadScope = config.EvaluationCheckScope;

        config = configurationContext.CheckConfig.FirstOrDefault(c => c.RuleId == _unusedPropertyRule.Id)
                 ?? CheckConfigurationEffective.Default;

        _unusedPropertyEnabled = config.IsEnabled;
        _unusedPropertyScope = config.EvaluationCheckScope;

        config = configurationContext.CheckConfig.FirstOrDefault(c => c.RuleId == _usedBeforeInitializedRule.Id)
                 ?? CheckConfigurationEffective.Default;

        _initializedAfterUseEnabled = config.IsEnabled;
        _initializedAfterUseScope = config.EvaluationCheckScope;

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

    public override void RegisterInternalActions(IInternalCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterPropertyReadAction(ProcessPropertyRead);

        if (_unusedPropertyEnabled || _initializedAfterUseEnabled)
        {
            registrationContext.RegisterPropertyWriteAction(ProcessPropertyWrite);
        }

        if (_unusedPropertyEnabled || _uninitializedReadEnabled)
        {
            registrationContext.RegisterProjectRequestProcessingDoneAction(DoneWithProject);
        }
    }

    private Dictionary<string, IMSBuildElementLocation?> _writenProperties = new(MSBuildNameIgnoreCaseComparer.Default);
    private HashSet<string> _readProperties = new(MSBuildNameIgnoreCaseComparer.Default);
    // For the 'Property Initialized after used' check - we are interested in cases where:
    //   1. Property is read anywhere and then initialized in the checked scope.
    //   2. Property is read in the checked scope and then initialized anywhere.
    private Dictionary<string, IMSBuildElementLocation> _uninitializedReadsInScope = new(MSBuildNameIgnoreCaseComparer.Default);
    private Dictionary<string, IMSBuildElementLocation> _uninitializedReadsOutOfScope = new(MSBuildNameIgnoreCaseComparer.Default);

    private void ProcessPropertyWrite(BuildCheckDataContext<PropertyWriteData> context)
    {
        PropertyWriteData writeData = context.Data;

        // If we want to track unused properties - store all definitions that are in scope.
        if (_unusedPropertyEnabled && CheckScopeClassifier.IsActionInObservedScope(_unusedPropertyScope,
                writeData.ElementLocation, writeData.ProjectFilePath))
        {
            _writenProperties[writeData.PropertyName] = writeData.ElementLocation;
        }

        if (_initializedAfterUseEnabled && !writeData.IsEmpty)
        {
            // For initialized after used check - we can remove the read from dictionary after hitting write - because
            //  once the property is written it should no more be uninitialized (so shouldn't be added again).

            if (_uninitializedReadsInScope.TryGetValue(writeData.PropertyName, out IMSBuildElementLocation? uninitInScopeReadLocation))
            {
                _uninitializedReadsInScope.Remove(writeData.PropertyName);

                context.ReportResult(BuildCheckResult.Create(
                    _initializedAfterUsedRule,
                    uninitInScopeReadLocation,
                    writeData.PropertyName, writeData.ElementLocation?.LocationString ?? string.Empty));
            }

            if (CheckScopeClassifier.IsActionInObservedScope(_initializedAfterUseScope,
                    writeData.ElementLocation, writeData.ProjectFilePath) &&
                _uninitializedReadsOutOfScope.TryGetValue(writeData.PropertyName, out IMSBuildElementLocation? uninitOutScopeReadLocation))
            {
                _uninitializedReadsOutOfScope.Remove(writeData.PropertyName);

                context.ReportResult(BuildCheckResult.Create(
                    _initializedAfterUsedRule,
                    uninitOutScopeReadLocation,
                    writeData.PropertyName, writeData.ElementLocation?.LocationString ?? string.Empty));
            }
        }
    }

    private void ProcessPropertyRead(BuildCheckDataContext<PropertyReadData> context)
    {
        PropertyReadData readData = context.Data;

        // Self property initialization is not considered as a violation.
        if (readData.PropertyReadContext != PropertyReadContext.PropertyEvaluationSelf &&
            // If we are interested in missing usage checking - let's store, regardless of location of read.
            _unusedPropertyEnabled)
        {
            _readProperties.Add(readData.PropertyName);
        }

        if (readData.IsUninitialized &&
            (_uninitializedReadEnabled || _initializedAfterUseEnabled) &&
            readData.PropertyReadContext != PropertyReadContext.PropertyEvaluationSelf &&
            readData.PropertyReadContext != PropertyReadContext.ConditionEvaluationWithOneSideEmpty &&
            (!_allowUninitPropsInConditions ||
             readData.PropertyReadContext != PropertyReadContext.ConditionEvaluation))
        {
            // We want to wait with reporting uninitialized reads until we are sure there wasn't later attempts to initialize them.
            if (_initializedAfterUseEnabled)
            {
                if (CheckScopeClassifier.IsActionInObservedScope(_initializedAfterUseScope,
                        readData.ElementLocation, readData.ProjectFilePath))
                {
                    _uninitializedReadsInScope[readData.PropertyName] = readData.ElementLocation;
                }
                // If uninitialized read happened in scope and out of scope - keep just that in scope.
                else if (!_uninitializedReadsInScope.ContainsKey(readData.PropertyName))
                {
                    _uninitializedReadsOutOfScope[readData.PropertyName] = readData.ElementLocation;
                }
            }
            else if (CheckScopeClassifier.IsActionInObservedScope(_uninitializedReadScope,
                         readData.ElementLocation, readData.ProjectFilePath))
            {
                // report immediately
                context.ReportResult(BuildCheckResult.Create(
                    _usedBeforeInitializedRule,
                    readData.ElementLocation,
                    readData.PropertyName));
            }
        }
    }


    private void DoneWithProject(BuildCheckDataContext<ProjectRequestProcessingDoneData> context)
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

        // Report the remaining uninitialized reads - as if 'initialized after read' check was enabled - we cannot report
        //  uninitialized reads immediately (instead we wait if they are attempted to be initialized late).
        foreach (var uninitializedRead in _uninitializedReadsInScope)
        {
            context.ReportResult(BuildCheckResult.Create(
                _usedBeforeInitializedRule,
                uninitializedRead.Value,
                uninitializedRead.Key));
        }

        _readProperties = new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
        _writenProperties = new Dictionary<string, IMSBuildElementLocation?>(MSBuildNameIgnoreCaseComparer.Default);
        _uninitializedReadsInScope = new Dictionary<string, IMSBuildElementLocation>(MSBuildNameIgnoreCaseComparer.Default);
    }
}
