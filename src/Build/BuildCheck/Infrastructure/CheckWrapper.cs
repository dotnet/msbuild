// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// A wrapping, enriching class for BuildCheck - so that we have additional data and functionality.
/// </summary>
internal sealed class CheckWrapper
{
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private readonly BuildCheckRuleTelemetryData[] _ruleTelemetryData;

    /// <summary>
    /// Maximum amount of messages that could be sent per check rule.
    /// </summary>
    public const int MaxReportsNumberPerRule = 20;

    /// <summary>
    /// Keeps track of number of reports sent per rule.
    /// </summary>
    private int _reportsCount = 0;

    /// <summary>
    /// Flags that this check should no more used and be deregistered.
    /// </summary>
    public bool IsThrottled { get; private set; } = false;

    /// <summary>
    /// Whether to limit number of reports for the Check.
    /// </summary>
    private readonly bool _limitReportsNumber = !Traits.Instance.EscapeHatches.DoNotLimitBuildCheckResultsNumber;

    public CheckWrapper(Check check)
    {
        Check = check;
        _ruleTelemetryData = new BuildCheckRuleTelemetryData[check.SupportedRules.Count];

        InitializeTelemetryData(_ruleTelemetryData, check);
    }

    private static void InitializeTelemetryData(BuildCheckRuleTelemetryData[] ruleTelemetryData, Check check)
    {
        int idx = 0;
        foreach (CheckRule checkRule in check.SupportedRules)
        {
            ruleTelemetryData[idx++] = new BuildCheckRuleTelemetryData(
                ruleId: checkRule.Id,
                checkFriendlyName: check.FriendlyName,
                isBuiltIn: check.IsBuiltIn,
                defaultSeverity: (checkRule.DefaultConfiguration.Severity ??
                                  CheckConfigurationEffective.Default.Severity).ToDiagnosticSeverity());
        }
    }

    internal Check Check { get; }

    private bool _areStatsInitialized = false;

    // Let's optimize for the scenario where users have a single .editorconfig file that applies to the whole solution.
    // In such case - configuration will be same for all projects. So we do not need to store it per project in a collection.
    internal CheckConfigurationEffective? CommonConfig { get; private set; }

    /// <summary>
    /// Ensures the check being configured for a new project (as each project can have different settings)
    /// </summary>
    /// <param name="fullProjectPath"></param>
    /// <param name="effectiveConfigs">Resulting merged configurations per rule (merged from check default and explicit user editorconfig).</param>
    /// <param name="editorConfigs">Configurations from editorconfig per rule.</param>
    internal void StartNewProject(
        string fullProjectPath,
        IReadOnlyList<CheckConfigurationEffective> effectiveConfigs,
        IReadOnlyList<CheckConfiguration> editorConfigs)
    {
        // Let's first update the telemetry data for the rules.
        int idx = 0;
        foreach (BuildCheckRuleTelemetryData ruleTelemetryData in _ruleTelemetryData)
        {
            CheckConfigurationEffective effectiveConfig = effectiveConfigs[Math.Max(idx, effectiveConfigs.Count - 1)];
            if (editorConfigs[idx].Severity != null)
            {
                ruleTelemetryData.ExplicitSeverities.Add(editorConfigs[idx].Severity!.Value.ToDiagnosticSeverity());
            }

            if (effectiveConfig.IsEnabled)
            {
                ruleTelemetryData.ProjectNamesWhereEnabled.Add(fullProjectPath);
            }

            idx++;
        }

        if (!_areStatsInitialized)
        {
            _areStatsInitialized = true;
            CommonConfig = effectiveConfigs[0];

            if (effectiveConfigs.Count == 1)
            {
                return;
            }
        }

        // The Common configuration is not common anymore - let's nullify it and we will need to fetch configuration per project.
        if (CommonConfig == null || !effectiveConfigs.All(t => t.IsSameConfigurationAs(CommonConfig)))
        {
            CommonConfig = null;
        }
    }

    internal void AddDiagnostic(CheckConfigurationEffective configurationEffective)
    {
        BuildCheckRuleTelemetryData? telemetryData =
            _ruleTelemetryData.FirstOrDefault(td => td.RuleId.Equals(configurationEffective.RuleId));

        if (telemetryData == null)
        {
            return;
        }

        switch (configurationEffective.Severity)
        {
            
            case CheckResultSeverity.Suggestion:
                telemetryData.IncrementMessagesCount();
                break;
            case CheckResultSeverity.Warning:
                telemetryData.IncrementWarningsCount();
                break;
            case CheckResultSeverity.Error:
                telemetryData.IncrementErrorsCount();
                break;
            case CheckResultSeverity.Default:
            case CheckResultSeverity.None:
            default:
                break;
        }

        // TODO: add throttling info - once it's merged
    }

    internal void ReportResult(BuildCheckResult result, ICheckContext checkContext, CheckConfigurationEffective config)
    {
        if (!IsThrottled)
        {
            _reportsCount++;
            BuildEventArgs eventArgs = result.ToEventArgs(config.Severity);
            eventArgs.BuildEventContext = checkContext.BuildEventContext;
            checkContext.DispatchBuildEvent(eventArgs);

            // Big amount of build check messages may lead to build hang.
            // See issue https://github.com/dotnet/msbuild/issues/10414
            // As a temporary fix, we will limit the number of messages that could be reported by the check.
            if (_limitReportsNumber)
            {
                if (_reportsCount >= MaxReportsNumberPerRule)
                {
                    IsThrottled = true;
                }
            }
        }
    }

    // to be used on eval node (BuildCheckDataSource.check)
    internal void UninitializeStats()
    {
        _areStatsInitialized = false;
    }

    internal IReadOnlyList<BuildCheckRuleTelemetryData> GetRuleTelemetryData()
    {
        foreach (BuildCheckRuleTelemetryData ruleTelemetryData in _ruleTelemetryData)
        {
            ruleTelemetryData.TotalRuntime = _stopwatch.Elapsed;
        }

        return _ruleTelemetryData;
    }

    internal TimeSpan Elapsed => _stopwatch.Elapsed;

    internal CleanupScope StartSpan()
    {
        _stopwatch.Start();
        return new CleanupScope(_stopwatch.Stop);
    }

    internal readonly struct CleanupScope(Action disposeAction) : IDisposable
    {
        public void Dispose() => disposeAction();
    }
}
