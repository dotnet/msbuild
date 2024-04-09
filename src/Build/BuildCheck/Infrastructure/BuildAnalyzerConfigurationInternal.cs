// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

/// <summary>
/// Counterpart type for BuildAnalyzerConfiguration - with all properties non-nullable
/// </summary>
internal sealed class BuildAnalyzerConfigurationInternal
{
    public BuildAnalyzerConfigurationInternal(string ruleId, EvaluationAnalysisScope evaluationAnalysisScope, BuildAnalyzerResultSeverity severity, bool isEnabled)
    {
        RuleId = ruleId;
        EvaluationAnalysisScope = evaluationAnalysisScope;
        Severity = severity;
        IsEnabled = isEnabled;
    }

    public string RuleId { get; }
    public EvaluationAnalysisScope EvaluationAnalysisScope { get; }
    public BuildAnalyzerResultSeverity Severity { get; }
    public bool IsEnabled { get; }

    // Intentionally not checking the RuleId
    //  as for analyzers with multiple rules, we can squash config to a single one,
    //  if the ruleId is the only thing differing.
    public bool IsSameConfigurationAs(BuildAnalyzerConfigurationInternal? other) =>
        other != null &&
        Severity == other.Severity &&
        IsEnabled == other.IsEnabled &&
        EvaluationAnalysisScope == other.EvaluationAnalysisScope;
}
