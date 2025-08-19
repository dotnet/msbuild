﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildAnalyzerConfiguration_Test
{
    [Fact]
    public void CreateWithNull_ReturnsObjectWithNullValues()
    {
        var buildConfig = BuildAnalyzerConfiguration.Create(null);
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBeNull();
    }

    [Fact]
    public void CreateWithEmpty_ReturnsObjectWithNullValues()
    {
        var buildConfig = BuildAnalyzerConfiguration.Create(new Dictionary<string, string>());
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("error", BuildAnalyzerResultSeverity.Error)]
    [InlineData("info", BuildAnalyzerResultSeverity.Info)]
    [InlineData("warning", BuildAnalyzerResultSeverity.Warning)]
    [InlineData("WARNING", BuildAnalyzerResultSeverity.Warning)]
    public void CreateBuildAnalyzerConfiguration_Severity(string parameter, BuildAnalyzerResultSeverity? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity" , parameter },
        };
        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBe(expected);

        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    public void CreateBuildAnalyzerConfiguration_IsEnabled(string parameter, bool? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "isenabled" , parameter },
        };

        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.IsEnabled.ShouldBe(expected);

        buildConfig.Severity.ShouldBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("ProjectOnly", EvaluationAnalysisScope.ProjectOnly)]
    [InlineData("ProjectWithImportsFromCurrentWorkTree", EvaluationAnalysisScope.ProjectWithImportsFromCurrentWorkTree)]
    [InlineData("ProjectWithImportsWithoutSdks", EvaluationAnalysisScope.ProjectWithImportsWithoutSdks)]
    [InlineData("ProjectWithAllImports", EvaluationAnalysisScope.ProjectWithAllImports)]
    [InlineData("projectwithallimports", EvaluationAnalysisScope.ProjectWithAllImports)]
    public void CreateBuildAnalyzerConfiguration_EvaluationAnalysisScope(string parameter, EvaluationAnalysisScope? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "evaluationanalysisscope" , parameter },
        };

        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBe(expected);

        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.Severity.ShouldBeNull();
    }

    [Theory]
    [InlineData("evaluationanalysisscope", "incorrec-value")]
    [InlineData("isenabled", "incorrec-value")]
    [InlineData("severity", "incorrec-value")]
    public void CreateBuildAnalyzerConfiguration_ExceptionOnInvalidInputValue(string key, string value)
    {
        var config = new Dictionary<string, string>()
        {
            { key , value },
        };

        var exception = Should.Throw<BuildCheckConfigurationException>(() => {
            BuildAnalyzerConfiguration.Create(config);
        });
        exception.Message.ShouldContain($"Incorrect value provided in config for key {key}");
    }
}
