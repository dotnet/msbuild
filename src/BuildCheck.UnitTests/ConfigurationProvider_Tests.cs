// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class ConfigurationProvider_Tests
{
    [Fact]
    public void GetRuleIdConfiguration_ReturnsEmptyConfig()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        test_key=test_value_updated
        """);

        var configurationProvider = new ConfigurationProvider();
        var configs = configurationProvider.GetConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        // empty
        configs.ShouldBe(new Dictionary<string, string>());
    }

    [Fact]
    public void GetRuleIdConfiguration_ReturnsConfiguration()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        """);

        var configurationProvider = new ConfigurationProvider();
        var configs = configurationProvider.GetConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        configs.Keys.Count.ShouldBe(2);

        configs.ContainsKey("property1").ShouldBeTrue();
        configs.ContainsKey("property2").ShouldBeTrue();

        configs["property2"].ShouldBe("value2");
        configs["property1"].ShouldBe("value1");
    }

    [Fact]
    public void GetRuleIdConfiguration_CustomConfigurationData()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true
        build_check.rule_id.scope=project
        build_check.rule_id.severity=default
        any_other_key1=any_other_value1
        any_other_key2=any_other_value2
        any_other_key3=any_other_value3
        any_other_key3=any_other_value3
        """);

        var configurationProvider = new ConfigurationProvider();
        var customConfiguration = configurationProvider.GetCustomConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");
        var configs = customConfiguration.ConfigurationData;

        configs!.Keys.Count().ShouldBe(3);

        configs.ContainsKey("property1").ShouldBeTrue();
        configs.ContainsKey("property2").ShouldBeTrue();
        configs.ContainsKey("is_enabled_2").ShouldBeTrue();
    }

    [Fact]
    public void GetRuleIdConfiguration_ReturnsBuildRuleConfiguration()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.severity=error
        build_check.rule_id.scope=projectfile
        """);

        var configurationProvider = new ConfigurationProvider();
        var buildConfig = configurationProvider.GetUserConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        buildConfig.ShouldNotBeNull();

        buildConfig.IsEnabled.ShouldBe(true);
        buildConfig.Severity.ShouldBe(BuildAnalyzerResultSeverity.Error);
        buildConfig.EvaluationAnalysisScope.ShouldBe(EvaluationAnalysisScope.ProjectFileOnly);
    }

    [Fact]
    public void GetRuleIdConfiguration_CustomConfigurationValidity_NotValid_DifferentValues()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true

        [test123.csproj]
        build_check.rule_id.property1=value2
        build_check.rule_id.property2=value3
        build_check.rule_id.is_enabled_2=tru1
        """);

        var configurationProvider = new ConfigurationProvider();
        configurationProvider.GetCustomConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        // should not fail => configurations are the same
        Should.Throw<BuildCheckConfigurationException>(() =>
        {
            configurationProvider.CheckCustomConfigurationDataValidity(Path.Combine(workFolder1.Path, "test123.csproj"), "rule_id");
        });
    }

    [Fact]
    public void GetRuleIdConfiguration_CustomConfigurationValidity_NotValid_DifferentKeys()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true

        [test123.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true
        build_check.rule_id.is_enabled_3=true
        """);

        var configurationProvider = new ConfigurationProvider();
        configurationProvider.GetCustomConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        // should not fail => configurations are the same
        Should.Throw<BuildCheckConfigurationException>(() =>
        {
            configurationProvider.CheckCustomConfigurationDataValidity(Path.Combine(workFolder1.Path, "test123.csproj"), "rule_id");
        });
    }

    [Fact]
    public void GetRuleIdConfiguration_CustomConfigurationValidity_Valid()
    {
        using TestEnvironment testEnvironment = TestEnvironment.Create();

        TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
        TransientTestFile config1 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
        """
        root=true

        [*.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true

        [test123.csproj]
        build_check.rule_id.property1=value1
        build_check.rule_id.property2=value2
        build_check.rule_id.is_enabled_2=true
        """);

        var configurationProvider = new ConfigurationProvider();
        configurationProvider.GetCustomConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

        // should fail, because the configs are the different
        Should.NotThrow(() =>
        {
            configurationProvider.CheckCustomConfigurationDataValidity(Path.Combine(workFolder1.Path, "test123.csproj"), "rule_id");
        });
    }

    [Theory]
    [InlineData(BuildAnalyzerResultSeverity.Warning, BuildAnalyzerResultSeverity.Warning, true)]
    [InlineData(BuildAnalyzerResultSeverity.Error, BuildAnalyzerResultSeverity.Error, true)]
    [InlineData(BuildAnalyzerResultSeverity.Default, BuildAnalyzerResultSeverity.Warning, true)]
    [InlineData(BuildAnalyzerResultSeverity.Suggestion, BuildAnalyzerResultSeverity.Suggestion, true)]
    [InlineData(BuildAnalyzerResultSeverity.None, BuildAnalyzerResultSeverity.None, false)]
    [InlineData(null, BuildAnalyzerResultSeverity.Warning, true)]
    public void GetConfigurationProvider_MergesSeverity_Correctly(BuildAnalyzerResultSeverity? buildAnalyzerResultSeverity, BuildAnalyzerResultSeverity expectedSeverity, bool expectedEnablment)
    {
        var configurationProvider = new ConfigurationProvider();
        BuildAnalyzerConfiguration buildAnalyzerConfiguration = new BuildAnalyzerConfiguration()
        {
            Severity = buildAnalyzerResultSeverity
        };

        BuildAnalyzerConfiguration defaultValue = new BuildAnalyzerConfiguration()
        {
            Severity = BuildAnalyzerResultSeverity.Warning
        };

        var internalBuildAnalyzer = configurationProvider.MergeConfiguration("ruleId", defaultValue, buildAnalyzerConfiguration);
        internalBuildAnalyzer.Severity.ShouldBe(expectedSeverity);
        internalBuildAnalyzer.IsEnabled.ShouldBe(expectedEnablment);
    }
}
