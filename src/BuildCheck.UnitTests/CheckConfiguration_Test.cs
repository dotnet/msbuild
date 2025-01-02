// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class CheckConfiguration_Test
{
    [Fact]
    public void CreateWithNull_ReturnsObjectWithNullValues()
    {
        var buildConfig = CheckConfiguration.Create(null);
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [Fact]
    public void CreateWithEmpty_ReturnsObjectWithNullValues()
    {
        var buildConfig = CheckConfiguration.Create(new Dictionary<string, string>());
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [Theory]
    [InlineData("error", CheckResultSeverity.Error)]
    [InlineData("ERROR", CheckResultSeverity.Error)]
    [InlineData("suggestion", CheckResultSeverity.Suggestion)]
    [InlineData("SUGGESTION", CheckResultSeverity.Suggestion)]
    [InlineData("warning", CheckResultSeverity.Warning)]
    [InlineData("WARNING", CheckResultSeverity.Warning)]
    [InlineData("NONE", CheckResultSeverity.None)]
    [InlineData("none", CheckResultSeverity.None)]
    [InlineData("default", CheckResultSeverity.Default)]
    [InlineData("DEFAULT", CheckResultSeverity.Default)]
    public void CreateCheckConfiguration_Severity(string parameter, CheckResultSeverity? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity" , parameter },
        };

        var buildConfig = CheckConfiguration.Create(config);

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
    public void CreateCheckConfiguration_SeverityAndEnabledOrder(string parameter, bool? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity", parameter },
        };

        var buildConfig = CheckConfiguration.Create(config);

        buildConfig.IsEnabled.ShouldBe(expected);
    }

    [Theory]
    [InlineData("project_file", EvaluationCheckScope.ProjectFileOnly)]
    [InlineData("projectfile", EvaluationCheckScope.ProjectFileOnly)]
    [InlineData("PROJECT_FILE", EvaluationCheckScope.ProjectFileOnly)]
    [InlineData("work_tree_imports", EvaluationCheckScope.WorkTreeImports)]
    [InlineData("WORK_TREE_IMPORTS", EvaluationCheckScope.WorkTreeImports)]
    [InlineData("all", EvaluationCheckScope.All)]
    [InlineData("ALL", EvaluationCheckScope.All)]
    public void CreateCheckConfiguration_EvaluationCheckScope(string parameter, EvaluationCheckScope? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "scope" , parameter },
        };

        var buildConfig = CheckConfiguration.Create(config);

        buildConfig.ShouldNotBeNull();
        buildConfig.EvaluationCheckScope.ShouldBe(expected);

        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.Severity.ShouldBeNull();
    }

    [Theory]
    [InlineData("scope", "incorrec-value")]
    [InlineData("severity", "incorrec-value")]
    public void CreateCheckConfiguration_ExceptionOnInvalidInputValue(string key, string value)
    {
        var config = new Dictionary<string, string>()
        {
            { key , value },
        };

        var exception = Should.Throw<BuildCheckConfigurationException>(() =>
        {
            CheckConfiguration.Create(config);
        });
        exception.Message.ShouldContain($"Incorrect value provided in config for key {key}");
    }
}
