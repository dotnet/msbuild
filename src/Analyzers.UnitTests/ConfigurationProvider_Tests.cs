// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCop.Infrastructure;
using Microsoft.Build.BuildCop.Infrastructure.EditorConfig;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.BuildCop.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

namespace Microsoft.Build.Analyzers.UnitTests
{
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
            msbuild_analyzer.rule_id.property1=value1
            msbuild_analyzer.rule_id.property2=value2
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
            msbuild_analyzer.rule_id.property1=value1
            msbuild_analyzer.rule_id.property2=value2
            msbuild_analyzer.rule_id.isEnabled=true
            msbuild_analyzer.rule_id.isEnabled2=true
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
            configs.ContainsKey("isenabled2").ShouldBeTrue();
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
            msbuild_analyzer.rule_id.isEnabled=true
            msbuild_analyzer.rule_id.Severity=Error
            msbuild_analyzer.rule_id.EvaluationAnalysisScope=AnalyzedProjectOnly
            """);

            var configurationProvider = new ConfigurationProvider();
            var buildConfig = configurationProvider.GetUserConfiguration(Path.Combine(workFolder1.Path, "test.csproj"), "rule_id");

            buildConfig.ShouldNotBeNull();

            buildConfig.IsEnabled?.ShouldBeTrue();
            buildConfig.Severity?.ShouldBe(BuildAnalyzerResultSeverity.Error);
            buildConfig.EvaluationAnalysisScope?.ShouldBe(EvaluationAnalysisScope.AnalyzedProjectOnly);
        }
    }
}
