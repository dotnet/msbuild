// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.Engine.UnitTests;

public sealed class FrameworkPackageTargets_Tests
{
    private static readonly string s_frameworkTargetsPath = Path.Combine(
        AppContext.BaseDirectory,
        "TestAssets",
        "Microsoft.Build.Framework.targets");

    private readonly ITestOutputHelper _output;

    public FrameworkPackageTargets_Tests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("netcoreapp2.0", "net10.0")]
    [InlineData("net9.0", "net10.0")]
    [InlineData("net461", "net472")]
    [InlineData("net471", "net472")]
    public void UnsupportedTargetFrameworkWarns(string targetFramework, string minimumSupportedTargetFramework)
    {
        MockLogger logger = BuildProject(targetFramework);

        BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
        warning.Message.ShouldNotBeNull();
        warning.Message.ShouldContain(targetFramework);
        warning.Message.ShouldContain(minimumSupportedTargetFramework);
    }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    [InlineData("net472")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public void SupportedOrNonRuntimeTargetFrameworkDoesNotWarn(string targetFramework)
    {
        MockLogger logger = BuildProject(targetFramework);

        logger.AssertNoWarnings();
    }

    [Fact]
    public void TargetFrameworkWarningCanBeSuppressed()
    {
        MockLogger logger = BuildProject("net9.0", suppressTfmSupportBuildWarnings: true);

        logger.AssertNoWarnings();
    }

    [Theory]
    [InlineData("PublishAot")]
    [InlineData("PublishTrimmed")]
    public void TrimmedPublishReceivesFeatureSwitchDefaults(string publishProperty)
    {
        string projectContents = $$"""
            <Project>
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
                <TargetFrameworkIdentifier>.NETCoreApp</TargetFrameworkIdentifier>
                <{{publishProperty}}>true</{{publishProperty}}>
              </PropertyGroup>
              <Import Project="{{s_frameworkTargetsPath}}" />
            </Project>
            """;

        using ProjectCollection projectCollection = new();
        using ProjectFromString project = new(projectContents, null, null, projectCollection);

        project.Project.GetPropertyValue("MicrosoftBuildEnableCustomPluginProbing").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildEnableAllPropertyFunctions").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildRestrictPropertyFunctionReceivers").ShouldBe("true");
        project.Project.GetPropertyValue("MicrosoftBuildEnableSdkResolverDynamicLoading").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildEnableConfigurationFileToolsets").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildEnableReflectiveTaskExecution").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildEnableReflectiveTaskParameterTypes").ShouldBe("false");
        project.Project.GetPropertyValue("MicrosoftBuildEnableReflectiveLoggerLoading").ShouldBe("false");

        project.Project.GetItems("RuntimeHostConfigurationOption")
            .Select(item => item.EvaluatedInclude)
            .ShouldBe(
                [
                    "Microsoft.Build.EnableCustomPluginProbing",
                    "Microsoft.Build.EnableAllPropertyFunctions",
                    "Microsoft.Build.RestrictPropertyFunctionReceivers",
                    "Microsoft.Build.EnableSdkResolverDynamicLoading",
                    "Microsoft.Build.EnableConfigurationFileToolsets",
                    "Microsoft.Build.EnableReflectiveTaskExecution",
                    "Microsoft.Build.EnableReflectiveTaskParameterTypes",
                    "Microsoft.Build.EnableReflectiveLoggerLoading",
                ]);
    }

    private MockLogger BuildProject(string targetFramework, bool suppressTfmSupportBuildWarnings = false)
    {
        string projectContents = $$"""
            <Project DefaultTargets="Build">
              <PropertyGroup>
                <TargetFramework>{{targetFramework}}</TargetFramework>
                <TargetFrameworkIdentifier>$([MSBuild]::GetTargetFrameworkIdentifier('$(TargetFramework)'))</TargetFrameworkIdentifier>
                <SuppressTfmSupportBuildWarnings>{{(suppressTfmSupportBuildWarnings ? "true" : "")}}</SuppressTfmSupportBuildWarnings>
              </PropertyGroup>
              <Import Project="{{s_frameworkTargetsPath}}" />
              <Target Name="Build" />
            </Project>
            """;

        return ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _output);
    }
}
