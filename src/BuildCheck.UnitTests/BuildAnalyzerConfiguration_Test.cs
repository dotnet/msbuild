// Licensed to the .NET Foundation under one or more agreements.
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
    [InlineData("ERROR", BuildAnalyzerResultSeverity.Error)]
    [InlineData("suggestion", BuildAnalyzerResultSeverity.Suggestion)]
    [InlineData("SUGGESTION", BuildAnalyzerResultSeverity.Suggestion)]
    [InlineData("warning", BuildAnalyzerResultSeverity.Warning)]
    [InlineData("WARNING", BuildAnalyzerResultSeverity.Warning)]
    [InlineData("NONE", BuildAnalyzerResultSeverity.None)]
    [InlineData("none", BuildAnalyzerResultSeverity.None)]
    [InlineData("default", BuildAnalyzerResultSeverity.Default)]
    [InlineData("DEFAULT", BuildAnalyzerResultSeverity.Default)]
    public void CreateBuildAnalyzerConfiguration_Severity(string parameter, BuildAnalyzerResultSeverity? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity" , parameter },
        };

        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBe(expected);
        buildConfig.EvaluationAnalysisScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("error", true)]
    [InlineData("warning", true)]
    [InlineData("suggestion", true)]
    [InlineData("none", false)]
    [InlineData("default", null)]
    public void CreateBuildAnalyzerConfiguration_SeverityAndEnabledOrder(string parameter, bool? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity", parameter },
        };
        
        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.IsEnabled.ShouldBe(expected);
    }

    [Theory]
    [InlineData("projectfile", EvaluationAnalysisScope.ProjectFileOnly)]
    [InlineData("PROJECTFILE", EvaluationAnalysisScope.ProjectFileOnly)]
    [InlineData("work_tree_imports", EvaluationAnalysisScope.WorkTreeImports)]
    [InlineData("WORK_TREE_IMPORTS", EvaluationAnalysisScope.WorkTreeImports)]
    [InlineData("all", EvaluationAnalysisScope.All)]
    [InlineData("ALL", EvaluationAnalysisScope.All)]
    public void CreateBuildAnalyzerConfiguration_EvaluationAnalysisScope(string parameter, EvaluationAnalysisScope? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "scope" , parameter },
        };

        var buildConfig = BuildAnalyzerConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.EvaluationAnalysisScope.ShouldBe(expected);

        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.Severity.ShouldBeNull();
    }

    [Theory]
    [InlineData("scope", "incorrec-value")]
    [InlineData("severity", "incorrec-value")]
    public void CreateBuildAnalyzerConfiguration_ExceptionOnInvalidInputValue(string key, string value)
    {
        var config = new Dictionary<string, string>()
        {
            { key , value },
        };

        var exception = Should.Throw<BuildCheckConfigurationException>(() =>
        {
            BuildAnalyzerConfiguration.Create(config);
        });
        exception.Message.ShouldContain($"Incorrect value provided in config for key {key}");
    }
}
