// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using System;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildAnalyzerConfigurationEffectiveTests
{
    [Theory]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectFileOnly, BuildAnalyzerResultSeverity.Warning,  true)]
    [InlineData("ruleId2", EvaluationAnalysisScope.ProjectFileOnly, BuildAnalyzerResultSeverity.Warning,  true)]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectFileOnly, BuildAnalyzerResultSeverity.Error, false)]
    public void IsSameConfigurationAsTest(
        string secondRuleId,
        EvaluationAnalysisScope secondScope,
        BuildAnalyzerResultSeverity secondSeverity,
        bool isExpectedToBeSame)
    {
        BuildAnalyzerConfigurationEffective configuration1 = new BuildAnalyzerConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectFileOnly,
                       severity: BuildAnalyzerResultSeverity.Warning);

        BuildAnalyzerConfigurationEffective configuration2 = new BuildAnalyzerConfigurationEffective(
            ruleId: secondRuleId,
            evaluationAnalysisScope: secondScope,
            severity: secondSeverity);

        configuration1.IsSameConfigurationAs(configuration2).ShouldBe(isExpectedToBeSame);
    }

    [Theory]
    [InlineData( BuildAnalyzerResultSeverity.Warning, true)]
    [InlineData(BuildAnalyzerResultSeverity.Suggestion, true)]
    [InlineData(BuildAnalyzerResultSeverity.Error, true)]
    [InlineData(BuildAnalyzerResultSeverity.None, false)]
    public void BuildAnalyzerConfigurationInternal_Constructor_SeverityConfig(BuildAnalyzerResultSeverity severity, bool isEnabledExpected)
    {
        BuildAnalyzerConfigurationEffective configuration = new BuildAnalyzerConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectFileOnly,
                       severity: severity);

        configuration.IsEnabled.ShouldBe(isEnabledExpected);
    }

    [Fact]
    public void BuildAnalyzerConfigurationInternal_Constructor_SeverityConfig_Fails()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            new BuildAnalyzerConfigurationEffective(
                        ruleId: "ruleId",
                        evaluationAnalysisScope: EvaluationAnalysisScope.ProjectFileOnly,
                        severity: BuildAnalyzerResultSeverity.Default);
        });
    }
}
