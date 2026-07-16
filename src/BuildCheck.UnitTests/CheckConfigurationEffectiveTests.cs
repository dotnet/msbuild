// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;

namespace Microsoft.Build.BuildCheck.UnitTests;

[TestClass]
public class CheckConfigurationEffectiveTests
{
    [MSBuildTestMethod]
    [DataRow("ruleId", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Warning, true)]
    [DataRow("ruleId2", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Warning, true)]
    [DataRow("ruleId", EvaluationCheckScope.ProjectFileOnly, CheckResultSeverity.Error, false)]
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

    [MSBuildTestMethod]
    [DataRow(CheckResultSeverity.Warning, true)]
    [DataRow(CheckResultSeverity.Suggestion, true)]
    [DataRow(CheckResultSeverity.Error, true)]
    [DataRow(CheckResultSeverity.None, false)]
    public void CheckConfigurationInternal_Constructor_SeverityConfig(CheckResultSeverity severity, bool isEnabledExpected)
    {
        CheckConfigurationEffective configuration = new CheckConfigurationEffective(
                       ruleId: "ruleId",
                       evaluationCheckScope: EvaluationCheckScope.ProjectFileOnly,
                       severity: severity);

        configuration.IsEnabled.ShouldBe(isEnabledExpected);
    }

    [MSBuildTestMethod]
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
