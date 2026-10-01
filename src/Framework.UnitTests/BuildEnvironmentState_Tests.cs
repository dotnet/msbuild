// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class BuildEnvironmentState_Tests
{
    internal static TestEnvironment CreateEnvironmentWithoutHost()
    {
        TestEnvironment env = TestEnvironment.Create();
        foreach (string variable in BuildEnvironmentState.AutomatedEnvironmentVariables)
        {
            env.SetEnvironmentVariable(variable, null);
        }

        env.SetEnvironmentVariable("CI", null);
        env.SetEnvironmentVariable("GITHUB_ACTIONS", null);
        env.SetEnvironmentVariable("MSBUILD_HOST_NAME", null);
        env.SetEnvironmentVariable("VSCODE_CWD", null);
        env.SetEnvironmentVariable("TERM_PROGRAM", null);
        return env;
    }

    [Fact]
    public void NoHostDetected()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();

        BuildEnvironmentState.GetHostName().ShouldBeNull();
    }

    [Theory]
    [InlineData("TF_BUILD", "True", BuildEnvironmentState.AzureDevOpsHostName)]
    [InlineData("TF_BUILD", "1", BuildEnvironmentState.AzureDevOpsHostName)]
    [InlineData("GITHUB_ACTIONS", "true", BuildEnvironmentState.GitHubActionsHostName)]
    [InlineData("TERM_PROGRAM", "vscode", "VSCode")]
    public void DetectsHost(string variable, string value, string expectedHost)
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable(variable, value);

        BuildEnvironmentState.GetHostName().ShouldBe(expectedHost);
    }

    [Fact]
    public void HostValuesMatchDocumentedContract()
    {
        // These values are part of the documented BuildEngineHost contract (documentation/VS-Telemetry-Data.md).
        BuildEnvironmentState.AzureDevOpsHostName.ShouldBe("Azure DevOps");
        BuildEnvironmentState.GitHubActionsHostName.ShouldBe("GitHub Action");
    }

    [Fact]
    public void GenericCIFlagDoesNotIdentifyAHost()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable("CI", "true");

        BuildEnvironmentState.GetHostName().ShouldBeNull();
    }

    [Fact]
    public void ExplicitHostNameWinsOverCI()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        env.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
        env.SetEnvironmentVariable("MSBUILD_HOST_NAME", "CustomHost");

        BuildEnvironmentState.GetHostName().ShouldBe("CustomHost");
    }

    [Fact]
    public void AzureDevOpsWinsOverGitHubActions()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        env.SetEnvironmentVariable("GITHUB_ACTIONS", "true");

        BuildEnvironmentState.GetHostName().ShouldBe(BuildEnvironmentState.AzureDevOpsHostName);
    }

    [Fact]
    public void CIWinsOverVSCode()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable("VSCODE_CWD", "folder");
        env.SetEnvironmentVariable("GITHUB_ACTIONS", "true");

        BuildEnvironmentState.GetHostName().ShouldBe(BuildEnvironmentState.GitHubActionsHostName);

        env.SetEnvironmentVariable("GITHUB_ACTIONS", "false");

        BuildEnvironmentState.GetHostName().ShouldBe("VSCode");
    }

    [Fact]
    public void VisualStudioWinsOverEverything()
    {
        using TestEnvironment env = CreateEnvironmentWithoutHost();
        env.SetEnvironmentVariable("MSBUILD_HOST_NAME", "CustomHost");
        env.SetEnvironmentVariable("TF_BUILD", "True");

        bool originalValue = BuildEnvironmentState.s_runningInVisualStudio;
        BuildEnvironmentState.s_runningInVisualStudio = true;
        try
        {
            BuildEnvironmentState.GetHostName().ShouldBe("VS");
        }
        finally
        {
            BuildEnvironmentState.s_runningInVisualStudio = originalValue;
        }
    }
}
