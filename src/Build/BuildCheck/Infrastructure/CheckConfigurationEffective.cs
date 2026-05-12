// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Counterpart type for BuildCheckConfiguration - with all properties non-nullable
/// </summary>
public sealed class CheckConfigurationEffective
{
    public CheckConfigurationEffective(string ruleId, EvaluationCheckScope evaluationCheckScope, CheckResultSeverity severity)
    {
        if (severity == CheckResultSeverity.Default)
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Severity 'Default' is not recognized by the BuildCheck reports infrastructure");
        }

        RuleId = ruleId;
        EvaluationCheckScope = evaluationCheckScope;
        Severity = severity;
    }

    internal static CheckConfigurationEffective Default { get; } =
        new(string.Empty, CheckConfiguration.Default.EvaluationCheckScope!.Value,
            CheckConfiguration.Default.Severity!.Value);

    public string RuleId { get; }

    public EvaluationCheckScope EvaluationCheckScope { get; }

    public CheckResultSeverity Severity { get; }

    public bool IsEnabled => Severity >= CheckResultSeverity.Suggestion;

    // Intentionally not checking the RuleId
    //  as for checks with multiple rules, we can squash config to a single one,
    //  if the ruleId is the only thing differing.
    public bool IsSameConfigurationAs(CheckConfigurationEffective? other) =>
        other != null &&
        Severity == other.Severity &&
        EvaluationCheckScope == other.EvaluationCheckScope;
}
