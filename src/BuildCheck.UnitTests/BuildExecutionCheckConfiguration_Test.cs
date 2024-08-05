// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildExecutionCheckConfiguration_Test
{
    [Fact]
    public void CreateWithNull_ReturnsObjectWithNullValues()
    {
        var buildConfig = BuildExecutionCheckConfiguration.Create(null);
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [Fact]
    public void CreateWithEmpty_ReturnsObjectWithNullValues()
    {
        var buildConfig = BuildExecutionCheckConfiguration.Create(new Dictionary<string, string>());
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("error", BuildExecutionCheckResultSeverity.Error)]
    [InlineData("ERROR", BuildExecutionCheckResultSeverity.Error)]
    [InlineData("suggestion", BuildExecutionCheckResultSeverity.Suggestion)]
    [InlineData("SUGGESTION", BuildExecutionCheckResultSeverity.Suggestion)]
    [InlineData("warning", BuildExecutionCheckResultSeverity.Warning)]
    [InlineData("WARNING", BuildExecutionCheckResultSeverity.Warning)]
    [InlineData("NONE", BuildExecutionCheckResultSeverity.None)]
    [InlineData("none", BuildExecutionCheckResultSeverity.None)]
    [InlineData("default", BuildExecutionCheckResultSeverity.Default)]
    [InlineData("DEFAULT", BuildExecutionCheckResultSeverity.Default)]
    public void CreateBuildExecutionCheckConfiguration_Severity(string parameter, BuildExecutionCheckResultSeverity? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity" , parameter },
        };

        var buildConfig = BuildExecutionCheckConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBe(expected);
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("error", true)]
    [InlineData("warning", true)]
    [InlineData("suggestion", true)]
    [InlineData("none", false)]
    [InlineData("default", null)]
    public void CreateBuildExecutionCheckConfiguration_SeverityAndEnabledOrder(string parameter, bool? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity", parameter },
        };
        
        var buildConfig = BuildExecutionCheckConfiguration.Create(config);

        buildConfig.IsEnabled.ShouldBe(expected);
    }

    [Theory]
    [InlineData("project", EvaluationCheckScope.ProjectOnly)]
    [InlineData("PROJECT", EvaluationCheckScope.ProjectOnly)]
    [InlineData("current_imports", EvaluationCheckScope.ProjectWithImportsFromCurrentWorkTree)]
    [InlineData("CURRENT_IMPORTS", EvaluationCheckScope.ProjectWithImportsFromCurrentWorkTree)]
    [InlineData("without_sdks", EvaluationCheckScope.ProjectWithImportsWithoutSdks)]
    [InlineData("WITHOUT_SDKS", EvaluationCheckScope.ProjectWithImportsWithoutSdks)]
    [InlineData("all", EvaluationCheckScope.ProjectWithAllImports)]
    [InlineData("ALL", EvaluationCheckScope.ProjectWithAllImports)]
    public void CreateBuildExecutionCheckConfiguration_EvaluationCheckScope(string parameter, EvaluationCheckScope? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "scope" , parameter },
        };

        var buildConfig = BuildExecutionCheckConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.EvaluationCheckScope.ShouldBe(expected);

        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.Severity.ShouldBeNull();
    }

    [Theory]
    [InlineData("scope", "incorrec-value")]
    [InlineData("severity", "incorrec-value")]
    public void CreateBuildExecutionCheckConfiguration_ExceptionOnInvalidInputValue(string key, string value)
    {
        var config = new Dictionary<string, string>()
        {
            { key , value },
        };

        var exception = Should.Throw<BuildCheckConfigurationException>(() =>
        {
            BuildExecutionCheckConfiguration.Create(config);
        });
        exception.Message.ShouldContain($"Incorrect value provided in config for key {key}");
    }
}
