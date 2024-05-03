// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildAnalyzerConfigurationInternalTests
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
        BuildAnalyzerConfigurationInternal configuration1 = new BuildAnalyzerConfigurationInternal(
                       ruleId: "ruleId",
                       evaluationAnalysisScope: EvaluationAnalysisScope.ProjectOnly,
                       severity: BuildAnalyzerResultSeverity.Warning,
                       isEnabled: true);

        BuildAnalyzerConfigurationInternal configuration2 = new BuildAnalyzerConfigurationInternal(
            ruleId: secondRuleId,
            evaluationAnalysisScope: secondScope,
            severity: secondSeverity,
            isEnabled: secondEnabled);

        configuration1.IsSameConfigurationAs(configuration2).ShouldBe(isExpectedToBeSame);
    }
}
