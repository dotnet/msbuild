// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Telemetry data for a single build check rule.
/// </summary>
/// <param name="ruleId"></param>
/// <param name="checkFriendlyName"></param>
/// <param name="isBuiltIn"></param>
/// <param name="defaultSeverity"></param>
internal sealed class BuildCheckRuleTelemetryData(
    string ruleId,
    string checkFriendlyName,
    bool isBuiltIn,
    DiagnosticSeverity defaultSeverity)
{
    public BuildCheckRuleTelemetryData(
        string ruleId,
        string checkFriendlyName,
        bool isBuiltIn,
        DiagnosticSeverity defaultSeverity,
        HashSet<DiagnosticSeverity> explicitSeverities,
        HashSet<string> projectNamesWhereEnabled,
        int violationMessagesCount,
        int violationWarningsCount,
        int violationErrorsCount,
        bool isThrottled,
        TimeSpan totalRuntime) : this(ruleId, checkFriendlyName, isBuiltIn,
        defaultSeverity)
    {
        ExplicitSeverities = explicitSeverities;
        ProjectNamesWhereEnabled = projectNamesWhereEnabled;
        ViolationMessagesCount = violationMessagesCount;
        ViolationWarningsCount = violationWarningsCount;
        ViolationErrorsCount = violationErrorsCount;
        IsThrottled = isThrottled;
        TotalRuntime = totalRuntime;
    }

    public static BuildCheckRuleTelemetryData Merge(
        BuildCheckRuleTelemetryData data1,
        BuildCheckRuleTelemetryData data2)
    {
        if (data1.RuleId != data2.RuleId)
        {
            throw new InvalidOperationException("Cannot merge telemetry data for different rules.");
        }
        return new BuildCheckRuleTelemetryData(
            data1.RuleId,
            data1.CheckFriendlyName,
            data1.IsBuiltIn,
            data1.DefaultSeverity,
            new HashSet<DiagnosticSeverity>(data1.ExplicitSeverities.Union(data2.ExplicitSeverities)),
            new HashSet<string>(data1.ProjectNamesWhereEnabled.Union(data2.ProjectNamesWhereEnabled)),
            data1.ViolationMessagesCount + data2.ViolationMessagesCount,
            data1.ViolationWarningsCount + data2.ViolationWarningsCount,
            data1.ViolationErrorsCount + data2.ViolationErrorsCount,
            data1.IsThrottled || data2.IsThrottled,
            data1.TotalRuntime + data2.TotalRuntime);
    }

    public string RuleId { get; init; } = ruleId;
    public string CheckFriendlyName { get; init; } = checkFriendlyName;
    public bool IsBuiltIn { get; init; } = isBuiltIn;
    public DiagnosticSeverity DefaultSeverity { get; init; } = defaultSeverity;

    /// <summary>
    /// A set of explicitly set severities (through editorconfig(s)) for the rule. There can be multiple - as different projects can have different settings.
    /// </summary>
    public HashSet<DiagnosticSeverity> ExplicitSeverities { get; init; } = [];
    public HashSet<string> ProjectNamesWhereEnabled { get; init; } = [];
    public int ViolationMessagesCount { get; private set; }
    public int ViolationWarningsCount { get; private set; }
    public int ViolationErrorsCount { get; private set; }
    public int ViolationsCount => ViolationMessagesCount + ViolationWarningsCount + ViolationErrorsCount;
    public bool IsThrottled { get; private set; }
    public TimeSpan TotalRuntime { get; set; }

    public void IncrementMessagesCount() => ViolationMessagesCount++;
    public void IncrementWarningsCount() => ViolationWarningsCount++;
    public void IncrementErrorsCount() => ViolationErrorsCount++;
    public void SetThrottled() => IsThrottled = true;
}

