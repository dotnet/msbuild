// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class BuildEnvironmentState_Tests
{
    [Theory]
    [InlineData("", null, false)]
    [InlineData("CI=true", "CI", true)]
    [InlineData("BUILD_ID=42", "CI", true)]
    [InlineData("TF_BUILD=True", "Azure DevOps", true)]
    [InlineData("TF_BUILD=1", "Azure DevOps", true)]
    [InlineData("GITHUB_ACTIONS=true", "GitHub Action", true)]
    [InlineData("JENKINS_URL=http://jenkins", "Jenkins", true)]
    [InlineData("GITLAB_CI=true", "GitLab CI", true)]
    [InlineData("TERM_PROGRAM=vscode", "VSCode", false)]
    [InlineData("TF_BUILD=True;GITHUB_ACTIONS=true", "Azure DevOps", true)]
    [InlineData("GITHUB_ACTIONS=true;VSCODE_CWD=folder", "GitHub Action", true)]
    [InlineData("GITHUB_ACTIONS=false;VSCODE_CWD=folder", "VSCode", false)]
    [InlineData("TF_BUILD=True;GITHUB_ACTIONS=true;MSBUILD_HOST_NAME=CustomHost", "CustomHost", true)]
    public void DetectsHost(string variables, string? expectedHost, bool isCI)
    {
        using TestEnvironment env = TestEnvironment.Create();
        foreach ((string variable, _) in BuildEnvironmentState.CIHostVariables)
        {
            env.SetEnvironmentVariable(variable, null);
        }

        foreach (string variable in new[] { "CI", "BUILD_ID", "GITHUB_ACTIONS", "MSBUILD_HOST_NAME", "VSCODE_CWD", "TERM_PROGRAM" })
        {
            env.SetEnvironmentVariable(variable, null);
        }

        foreach (string assignment in variables.Split([';'], System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = assignment.Split('=');
            env.SetEnvironmentVariable(parts[0], parts[1]);
        }

        BuildEnvironmentState.GetHostName().ShouldBe(expectedHost);
        BuildEnvironmentState.IsAutomatedEnvironment().ShouldBe(isCI);
    }
}
