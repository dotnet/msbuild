// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;

namespace Microsoft.Build.BuildCheck.UnitTests;

[TestClass]
public class CheckConfiguration_Test
{
    [MSBuildTestMethod]
    public void CreateWithNull_ReturnsObjectWithNullValues()
    {
        var buildConfig = CheckConfiguration.Create(null);
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [MSBuildTestMethod]
    public void CreateWithEmpty_ReturnsObjectWithNullValues()
    {
        var buildConfig = CheckConfiguration.Create(new Dictionary<string, string>());
        buildConfig.ShouldNotBeNull();
        buildConfig.Severity.ShouldBeNull();
        buildConfig.IsEnabled.ShouldBeNull();
        buildConfig.EvaluationCheckScope.ShouldBeNull();
    }

    [MSBuildTestMethod]
    [DataRow("error", CheckResultSeverity.Error)]
    [DataRow("ERROR", CheckResultSeverity.Error)]
    [DataRow("suggestion", CheckResultSeverity.Suggestion)]
    [DataRow("SUGGESTION", CheckResultSeverity.Suggestion)]
    [DataRow("warning", CheckResultSeverity.Warning)]
    [DataRow("WARNING", CheckResultSeverity.Warning)]
    [DataRow("NONE", CheckResultSeverity.None)]
    [DataRow("none", CheckResultSeverity.None)]
    [DataRow("default", CheckResultSeverity.Default)]
    [DataRow("DEFAULT", CheckResultSeverity.Default)]
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

    [MSBuildTestMethod]
    [DataRow("error", true)]
    [DataRow("warning", true)]
    [DataRow("suggestion", true)]
    [DataRow("none", false)]
    [DataRow("default", null)]
    public void CreateCheckConfiguration_SeverityAndEnabledOrder(string parameter, bool? expected)
    {
        var config = new Dictionary<string, string>()
        {
            { "severity", parameter },
        };

        var buildConfig = CheckConfiguration.Create(config);

        buildConfig.IsEnabled.ShouldBe(expected);
    }

    [MSBuildTestMethod]
    [DataRow("project_file", EvaluationCheckScope.ProjectFileOnly)]
    [DataRow("projectfile", EvaluationCheckScope.ProjectFileOnly)]
    [DataRow("PROJECT_FILE", EvaluationCheckScope.ProjectFileOnly)]
    [DataRow("work_tree_imports", EvaluationCheckScope.WorkTreeImports)]
    [DataRow("WORK_TREE_IMPORTS", EvaluationCheckScope.WorkTreeImports)]
    [DataRow("all", EvaluationCheckScope.All)]
    [DataRow("ALL", EvaluationCheckScope.All)]
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

    [MSBuildTestMethod]
    [DataRow("scope", "incorrec-value")]
    [DataRow("severity", "incorrec-value")]
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
