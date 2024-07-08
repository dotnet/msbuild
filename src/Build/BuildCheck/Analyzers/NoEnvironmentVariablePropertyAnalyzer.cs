// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Experimental.BuildCheck.Analyzers;

internal sealed class NoEnvironmentVariablePropertyAnalyzer : BuildAnalyzer
{
    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule(
        "BC0103",
        "NoEnvironmentVariablePropertyAnalyzer",
        "No implicit property derived from an environment variable should be used during the build",
        "Property is derived from environment variable: '{0}' with value: '{1}'. Properties should be passed explicitly using the /p option.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.NoEnvironmentVariablePropertyAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // No custom configuration
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) => registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);

    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        if (context.Data.EvaluatedEnvironmentVariables.Count != 0)
        {
            foreach (var envVariableData in context.Data.EvaluatedEnvironmentVariables)
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    ElementLocation.Create(envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column),
                    envVariableData.Key,
                    envVariableData.Value.EnvVarValue));
            }
        }
    }
}
