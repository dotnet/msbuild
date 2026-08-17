// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using System;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class CheckConfigurationEffectiveTests
{
    [Theory]
    [InlineData("ruleId", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Warning, true)]
    [InlineData("ruleId2", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Warning, true)]
    [InlineData("ruleId", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Error, false)]
    public void IsSameConfigurationAsTest(
        string secondRuleId,
        EvaluationCheckScope secondScope,
        CheckResultSeverity secondSeverity,
        bool isExpectedToBeSame)
    {
        CheckConfigurationEffective configuration1 = new CheckConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationCheckScope: EvaluationCheckScope.ProjectFileOnly,
                       severity: CheckResultSeverity.Warning);

        CheckConfigurationEffective configuration2 = new CheckConfigurationEffective(
            ruleId: secondRuleId,
            evaluationCheckScope: secondScope,
            severity: secondSeverity);

        configuration1.IsSameConfigurationAs(configuration2).ShouldBe(isExpectedToBeSame);
    }

    [Theory]
    [InlineData(CheckResultSeverity.Warning, true)]
    [InlineData(CheckResultSeverity.Suggestion, true)]
    [InlineData(CheckResultSeverity.Error, true)]
    [InlineData(CheckResultSeverity.None, false)]
    public void CheckConfigurationInternal_Constructor_SeverityConfig(CheckResultSeverity severity, bool isEnabledExpected)
    {
        CheckConfigurationEffective configuration = new CheckConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationCheckScope: EvaluationCheckScope.ProjectFileOnly,
                       severity: severity);

        configuration.IsEnabled.ShouldBe(isEnabledExpected);
    }

    [Fact]
    public void CheckConfigurationInternal_Constructor_SeverityConfig_Fails()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            new CheckConfigurationEffective(
                        ruleId: "ruleId",
                        evaluationCheckScope: EvaluationCheckScope.ProjectFileOnly,
                        severity: CheckResultSeverity.Default);
        });
    }
}
