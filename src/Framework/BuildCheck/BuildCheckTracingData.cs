// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Wrapper for the tracing data to be transferred from the worker nodes to the central node.
/// </summary>
/// <param name="telemetryData"></param>
/// <param name="infrastructureTracingData"></param>
internal sealed class BuildCheckTracingData(
    Dictionary<string, BuildCheckRuleTelemetryData> telemetryData,
    Dictionary<string, TimeSpan> infrastructureTracingData)
{
    public BuildCheckTracingData(IReadOnlyList<BuildCheckRuleTelemetryData> telemetryData, Dictionary<string, TimeSpan> infrastructureTracingData)
        : this(telemetryData.ToDictionary(data => data.RuleId), infrastructureTracingData)
    { }

    public BuildCheckTracingData()
        : this(new Dictionary<string, BuildCheckRuleTelemetryData>(), [])
    { }

    internal BuildCheckTracingData(Dictionary<string, TimeSpan> executionData)
        : this(new Dictionary<string, BuildCheckRuleTelemetryData>(), executionData)
    { }

    public Dictionary<string, BuildCheckRuleTelemetryData> TelemetryData { get; private set; } = telemetryData;
    public Dictionary<string, TimeSpan> InfrastructureTracingData { get; private set; } = infrastructureTracingData;

    /// <summary>
    /// Gets the runtime stats per individual checks friendly names
    /// </summary>
    public Dictionary<string, TimeSpan> ExtractCheckStats() =>
        // Stats are per rule, while runtime is per check - and check can have multiple rules.
        // In case of multi-rule check, the runtime stats are duplicated for each rule.
        TelemetryData
            .GroupBy(d => d.Value.CheckFriendlyName)
            .ToDictionary(g => g.Key, g => g.First().Value.TotalRuntime);

    public void MergeIn(BuildCheckTracingData other)
    {
        InfrastructureTracingData.Merge(other.InfrastructureTracingData, (span1, span2) => span1 + span2);
        TelemetryData.Merge(other.TelemetryData, BuildCheckRuleTelemetryData.Merge);
    }
}
