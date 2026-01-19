// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Framework.Telemetry;

internal class BuildCheckTelemetry
{
    private const string FailedAcquisitionEventName = "buildcheck/acquisitionfailure";
    private const string RunEventName = "buildcheck/run";
    private const string RuleStatsEventName = "buildcheck/rule";
    private Guid _submissionId = Guid.NewGuid();

    /// <summary>
    /// Translates failed acquisition event to telemetry transport data.
    /// </summary>
    internal (string, IDictionary<string, string>) ProcessCustomCheckLoadingFailure(string assemblyName,
        Exception exception)
    {
        var properties = new Dictionary<string, string>();
        properties["SubmissionId"] = _submissionId.ToString();
        properties["AssemblyName"] = assemblyName;
        string? exceptionType = exception.GetType().FullName;
        if (exceptionType != null)
        {
            properties["ExceptionType"] = exceptionType;
        }
        if (exception.Message != null)
        {
            properties["ExceptionMessage"] = exception.Message;
        }

        return (FailedAcquisitionEventName, properties);
    }

    /// <summary>
    /// Translates BuildCheck tracing data to telemetry transport data.
    /// </summary>
    internal IEnumerable<(string, IDictionary<string, string>)> ProcessBuildCheckTracingData(BuildCheckTracingData data)
    {
        int rulesCount = data.TelemetryData.Count;
        int customRulesCount = data.TelemetryData.Count(t => !t.Value.IsBuiltIn);
        int violationsCount = data.TelemetryData.Sum(t => t.Value.ViolationsCount);
        long runtimeTicks = data.ExtractCheckStats().Sum(v => v.Value.Ticks);
        runtimeTicks += data.InfrastructureTracingData.Sum(v => v.Value.Ticks);
        TimeSpan totalRuntime = new TimeSpan(runtimeTicks);

        var properties = new Dictionary<string, string>();
        properties["SubmissionId"] = _submissionId.ToString();
        properties["RulesCount"] = rulesCount.ToString(CultureInfo.InvariantCulture);
        properties["CustomRulesCount"] = customRulesCount.ToString(CultureInfo.InvariantCulture);
        properties["ViolationsCount"] = violationsCount.ToString(CultureInfo.InvariantCulture);
        properties["TotalRuntimeInMilliseconds"] = totalRuntime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

        yield return (RunEventName, properties);

        foreach (BuildCheckRuleTelemetryData buildCheckRuleTelemetryData in data.TelemetryData.Values)
        {
            properties = new Dictionary<string, string>();
            properties["SubmissionId"] = _submissionId.ToString();
            properties["RuleId"] = buildCheckRuleTelemetryData.RuleId;
            properties["CheckFriendlyName"] = buildCheckRuleTelemetryData.CheckFriendlyName;
            properties["IsBuiltIn"] = buildCheckRuleTelemetryData.IsBuiltIn.ToString(CultureInfo.InvariantCulture);
            properties["DefaultSeverityId"] = ((int)buildCheckRuleTelemetryData.DefaultSeverity).ToString(CultureInfo.InvariantCulture);
            properties["DefaultSeverity"] = buildCheckRuleTelemetryData.DefaultSeverity.ToString();
            properties["EnabledProjectsCount"] = buildCheckRuleTelemetryData.ProjectNamesWhereEnabled.Count.ToString(CultureInfo.InvariantCulture);

            if (buildCheckRuleTelemetryData.ExplicitSeverities.Any())
            {
                properties["ExplicitSeverities"] = buildCheckRuleTelemetryData.ExplicitSeverities
                    .Select(s => s.ToString()).ToCsvString(false);
                properties["ExplicitSeveritiesIds"] = buildCheckRuleTelemetryData.ExplicitSeverities
                    .Select(s => ((int)s).ToString(CultureInfo.InvariantCulture)).ToCsvString(false);
            }

            properties["ViolationMessagesCount"] = buildCheckRuleTelemetryData.ViolationMessagesCount.ToString(CultureInfo.InvariantCulture);
            properties["ViolationWarningsCount"] = buildCheckRuleTelemetryData.ViolationWarningsCount.ToString(CultureInfo.InvariantCulture);
            properties["ViolationErrorsCount"] = buildCheckRuleTelemetryData.ViolationErrorsCount.ToString(CultureInfo.InvariantCulture);
            properties["IsThrottled"] = buildCheckRuleTelemetryData.IsThrottled.ToString(CultureInfo.InvariantCulture);
            properties["TotalRuntimeInMilliseconds"] = buildCheckRuleTelemetryData.TotalRuntime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

            yield return (RuleStatsEventName, properties);
        }

        // set for the new submission in case of build server
        _submissionId = Guid.NewGuid();
    }
}
