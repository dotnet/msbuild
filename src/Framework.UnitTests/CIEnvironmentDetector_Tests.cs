// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class CIEnvironmentDetector_Tests
{
    private static readonly string[] s_ciVariables =
    [
        "TF_BUILD",
        "GITHUB_ACTIONS",
        "APPVEYOR",
        "CI",
        "TRAVIS",
        "CIRCLECI",
        "CODEBUILD_BUILD_ID",
        "AWS_REGION",
        "BUILD_ID",
        "BUILD_URL",
        "PROJECT_ID",
        "TEAMCITY_VERSION",
        "JB_SPACE_API_URL",
    ];

    /// <summary>
    /// Removes every variable that identifies a CI system, so tests behave the same on developer machines and CI agents.
    /// </summary>
    internal static void ClearCIEnvironment(TestEnvironment env)
    {
        foreach (string variable in s_ciVariables)
        {
            env.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void NoCIVariablesIsNotCI()
    {
        using TestEnvironment env = TestEnvironment.Create();
        ClearCIEnvironment(env);

        CIEnvironmentDetector.IsCIEnvironment().ShouldBeFalse();
    }

    [Theory]
    [InlineData("TF_BUILD", "True", true)]
    [InlineData("TF_BUILD", "1", true)]
    [InlineData("TF_BUILD", "false", false)]
    [InlineData("TF_BUILD", "", false)]
    [InlineData("GITHUB_ACTIONS", "true", true)]
    [InlineData("GITHUB_ACTIONS", "0", false)]
    [InlineData("CI", "true", true)]
    [InlineData("APPVEYOR", "True", true)]
    [InlineData("TRAVIS", "true", true)]
    [InlineData("CIRCLECI", "true", true)]
    [InlineData("TEAMCITY_VERSION", "2024.1", true)]
    [InlineData("JB_SPACE_API_URL", "https://example.invalid", true)]
    public void SingleVariableDetection(string variable, string value, bool expected)
    {
        using TestEnvironment env = TestEnvironment.Create();
        ClearCIEnvironment(env);
        env.SetEnvironmentVariable(variable, value);

        CIEnvironmentDetector.IsCIEnvironment().ShouldBe(expected);
    }

    [Theory]
    [InlineData("BUILD_ID", "BUILD_URL")]
    [InlineData("BUILD_ID", "PROJECT_ID")]
    [InlineData("CODEBUILD_BUILD_ID", "AWS_REGION")]
    public void VariableGroupsRequireEveryVariable(string first, string second)
    {
        using TestEnvironment env = TestEnvironment.Create();
        ClearCIEnvironment(env);

        env.SetEnvironmentVariable(first, "value");
        CIEnvironmentDetector.IsCIEnvironment().ShouldBeFalse();

        env.SetEnvironmentVariable(second, "value");
        CIEnvironmentDetector.IsCIEnvironment().ShouldBeTrue();
    }
}
