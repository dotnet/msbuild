// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildAnalyzerConfigurationEffectiveTests
{
    [Theory]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Warning, true, true)]
    [InlineData("ruleId2", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Warning, true, true)]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Error, true, false)]
    public void IsSameConfigurationAsTest(
        string secondRuleId,
        EvaluationAnalysisScope secondScope,
        BuildAnalyzerResultSeverity secondSeverity,
        bool secondEnabled,
        bool isExpectedToBeSame)
    {
        BuildAnalyzerConfigurationEffective configuration1 = new BuildAnalyzerConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectOnly,
                       severity: BuildAnalyzerResultSeverity.Warning,
                       isEnabled: true);

        BuildAnalyzerConfigurationEffective configuration2 = new BuildAnalyzerConfigurationEffective(
            ruleId: secondRuleId,
            evaluationAnalysisScope: secondScope,
            severity: secondSeverity,
            isEnabled: secondEnabled);

        configuration1.IsSameConfigurationAs(configuration2).ShouldBe(isExpectedToBeSame);
    }
}
