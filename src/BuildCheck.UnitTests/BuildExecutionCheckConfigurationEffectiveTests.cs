// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using System;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildExecutionCheckConfigurationEffectiveTests
{
    [Theory]
    [InlineData("ruleId", EvaluationCheckScope.ProjectOnly, BuildExecutionCheckResultSeverity.Warning,  true)]
    [InlineData("ruleId2", EvaluationCheckScope.ProjectOnly, BuildExecutionCheckResultSeverity.Warning,  true)]
    [InlineData("ruleId", EvaluationCheckScope.ProjectOnly, BuildExecutionCheckResultSeverity.Error, false)]
    public void IsSameConfigurationAsTest(
        string secondRuleId,
        EvaluationCheckScope secondScope,
        BuildExecutionCheckResultSeverity secondSeverity,
        bool isExpectedToBeSame)
    {
        BuildExecutionCheckConfigurationEffective configuration1 = new BuildExecutionCheckConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationCheckScope: EvaluationCheckScope.ProjectOnly,
                       severity: BuildExecutionCheckResultSeverity.Warning);

        BuildExecutionCheckConfigurationEffective configuration2 = new BuildExecutionCheckConfigurationEffective(
            ruleId: secondRuleId,
            evaluationCheckScope: secondScope,
            severity: secondSeverity);

        configuration1.IsSameConfigurationAs(configuration2).ShouldBe(isExpectedToBeSame);
    }

    [Theory]
    [InlineData( BuildExecutionCheckResultSeverity.Warning, true)]
    [InlineData(BuildExecutionCheckResultSeverity.Suggestion, true)]
    [InlineData(BuildExecutionCheckResultSeverity.Error, true)]
    [InlineData(BuildExecutionCheckResultSeverity.None, false)]
    public void BuildExecutionCheckConfigurationInternal_Constructor_SeverityConfig(BuildExecutionCheckResultSeverity severity, bool isEnabledExpected)
    {
        BuildExecutionCheckConfigurationEffective configuration = new BuildExecutionCheckConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationCheckScope: EvaluationCheckScope.ProjectOnly,
                       severity: severity);

        configuration.IsEnabled.ShouldBe(isEnabledExpected);
    }

    [Fact]
    public void BuildExecutionCheckConfigurationInternal_Constructor_SeverityConfig_Fails()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            new BuildExecutionCheckConfigurationEffective(
                        ruleId: "ruleId",
                        evaluationCheckScope: EvaluationCheckScope.ProjectOnly,
                        severity: BuildExecutionCheckResultSeverity.Default);
        });
    }
}
