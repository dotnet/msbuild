// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using System;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildAnalyzerConfigurationInternalTests
{
    [Theory]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Warning,  true)]
    [InlineData("ruleId2", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Warning,  true)]
    [InlineData("ruleId", EvaluationAnalysisScope.ProjectOnly, BuildAnalyzerResultSeverity.Error, false)]
    public void IsSameConfigurationAsTest(
        string secondRuleId,
        EvaluationAnalysisScope secondScope,
        BuildAnalyzerResultSeverity secondSeverity,
        bool isExpectedToBeSame)
    {
        BuildAnalyzerConfigurationInternal configuration1 = new BuildAnalyzerConfigurationInternal(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectOnly,
                       severity: BuildAnalyzerResultSeverity.Warning);

        BuildAnalyzerConfigurationInternal configuration2 = new BuildAnalyzerConfigurationInternal(
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
        BuildAnalyzerConfigurationInternal configuration = new BuildAnalyzerConfigurationInternal(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectOnly,
                       severity: severity);

        configuration.IsEnabled.ShouldBe(isEnabledExpected);
    }

    [Fact]
    public void BuildAnalyzerConfigurationInternal_Constructor_SeverityConfig_Fails()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            new BuildAnalyzerConfigurationInternal(
                        ruleId: "ruleId",
                        evaluationAnalysisScope: EvaluationAnalysisScope.ProjectOnly,
                        severity: BuildAnalyzerResultSeverity.Default);
        });
    }
}
