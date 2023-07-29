// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static Microsoft.NET.Build.Containers.KnownStrings.Properties;
using FluentAssertions;
using Microsoft.Build.Execution;
using Xunit;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using System.Linq;

namespace Microsoft.NET.Build.Containers.Targets.IntegrationTests;

public class TargetsTests
{
    [InlineData(true, "/app/foo.exe")]
    [InlineData(false, "dotnet", "/app/foo.dll")]
    [Theory]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var (project, _, d) = ProjectInitializer.InitProject(new()
        {
            [UseAppHost] = useAppHost.ToString()
        }, projectName: $"{nameof(CanSetEntrypointArgsToUseAppHost)}_{useAppHost}_{String.Join("_", entrypointArgs)}");
        using var _ = d;
        Assert.True(project.Build(ComputeContainerConfig));
        var computedEntrypointArgs = project.GetItems(ContainerEntrypoint).Select(i => i.EvaluatedInclude).ToArray();
        foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
        {
            Assert.Equal(First, Second);
        }
    }

    [InlineData("WebApplication44", "webapplication44", true)]
    [InlineData("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [InlineData("*friendly-suspicious-alligator", "", false)]
    [InlineData("web/app2+7", "web/app2-7", true)]
    [InlineData("Microsoft.Apps.Demo.ContosoWeb", "microsoft-apps-demo-contosoweb", true)]
    [Theory]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            [AssemblyName] = projectName
        }, projectName: $"{nameof(CanNormalizeInputContainerNames)}_{projectName}_{expectedContainerImageName}_{shouldPass}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[]{ logger }, null, out var outputs).Should().Be(shouldPass, String.Join(Environment.NewLine, logger.AllMessages));
        Assert.Equal(expectedContainerImageName, instance.GetPropertyValue(ContainerImageName));
    }

    [InlineData("7.0.100", true)]
    [InlineData("8.0.100", true)]
    [InlineData("7.0.100-preview.7", true)]
    [InlineData("7.0.100-rc.1", true)]
    [InlineData("6.0.100", false)]
    [InlineData("7.0.100-preview.1", false)]
    [Theory]
    public void CanWarnOnInvalidSDKVersions(string sdkVersion, bool isAllowed)
    {
        var (project, _, d) = ProjectInitializer.InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanWarnOnInvalidSDKVersions)}_{sdkVersion}_{isAllowed}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        var derivedIsAllowed = Boolean.Parse(project.GetProperty("_IsSDKContainerAllowedVersion").EvaluatedValue);
        // var buildResult = instance.Build(new[]{"_ContainerVerifySDKVersion"}, null, null, out var outputs);
        derivedIsAllowed.Should().Be(isAllowed, $"SDK version {(isAllowed ? "should" : "should not")} have been allowed ");
    }

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void GetsConventionalLabelsByDefault(bool shouldEvaluateLabels)
    {
        var (project, _, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerGenerateLabels] = shouldEvaluateLabels.ToString()
        }, projectName: $"{nameof(GetsConventionalLabelsByDefault)}_{shouldEvaluateLabels}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue("Build should have succeeded");
        if (shouldEvaluateLabels)
        {
            instance.GetItems(ContainerLabel).Should().NotBeEmpty("Should have evaluated some labels by default");
        }
        else
        {
            instance.GetItems(ContainerLabel).Should().BeEmpty("Should not have evaluated any labels by default");
        }
    }

    private static bool LabelMatch(string label, string value, ProjectItemInstance item) => item.EvaluatedInclude == label && item.GetMetadata("Value") is { } v && v.EvaluatedValue == value;

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
    public void ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn(bool includeSourceControl)
    {
        var commitHash = "abcdef";
        var repoUrl = "https://git.cosmere.com/shard/whimsy.git";

        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["PublishRepositoryUrl"] = includeSourceControl.ToString(),
            ["PrivateRepositoryUrl"] = repoUrl,
            ["SourceRevisionId"] = commitHash
        }, projectName: $"{nameof(ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn)}_{includeSourceControl}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue("Build should have succeeded but failed due to {0}", String.Join("\n", logger.AllMessages));
        var labels = instance.GetItems(ContainerLabel);
        if (includeSourceControl)
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        }
        else
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.NotContain(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.NotContain(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        };
    }

    [InlineData("7.0.100", "v7.0", "7.0")]
    [InlineData("7.0.100-preview.7", "v7.0", "7.0")]
    [InlineData("7.0.100-rc.1", "v7.0", "7.0")]
    [InlineData("8.0.100", "v8.0", "8.0")]
    [InlineData("8.0.100", "v7.0", "7.0")]
    [InlineData("8.0.100-preview.7", "v8.0", "8.0-preview.7")]
    [InlineData("8.0.100-rc.1", "v8.0", "8.0-rc.1")]
    [InlineData("8.0.100-rc.1", "v7.0", "7.0")]
    [InlineData("8.0.200", "v8.0", "8.0")]
    [InlineData("8.0.200", "v7.0", "7.0")]
    [InlineData("8.0.200-preview3", "v7.0", "7.0")]
    [InlineData("8.0.200-preview3", "v8.0", "8.0")]
    [InlineData("6.0.100", "v6.0", "6.0")]
    [InlineData("6.0.100-preview.1", "v6.0", "6.0")]
    [InlineData("8.0.100-dev", "v8.0", "8.0-preview")]
    [InlineData("8.0.100-ci", "v8.0", "8.0-preview")]
    [InlineData("8.0.100-alpha.12345", "v8.0", "8.0-preview.1")]
    [InlineData("9.0.100-alpha.12345", "v9.0", "9.0-preview.1")]
    [Theory]
    public void CanComputeTagsForSupportedSDKVersions(string sdkVersion, string tfm, string expectedTag)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["TargetFrameworkVersion"] = tfm,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanComputeTagsForSupportedSDKVersions)}_{sdkVersion}_{tfm}_{expectedTag}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[]{"_ComputeContainerBaseImageTag"}, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedTag = instance.GetProperty("_ContainerBaseImageTag").EvaluatedValue;
        computedTag.Should().Be(expectedTag);
    }

    [InlineData("v8.0", "linux-x64", "64198")]
    [InlineData("v8.0", "win-x64", "ContainerUser")]
    [InlineData("v7.0", "linux-x64", null)]
    [InlineData("v7.0", "win-x64", null)]
    [InlineData("v9.0", "linux-x64", "64198")]
    [InlineData("v9.0", "win-x64", "ContainerUser")]
    [Theory]
    public void CanComputeContainerUser(string tfm, string rid, string expectedUser)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["TargetFrameworkVersion"] = tfm,
            ["ContainerRuntimeIdentifier"] = rid
        }, projectName: $"{nameof(CanComputeTagsForSupportedSDKVersions)}_{tfm}_{rid}_{expectedUser}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedTag = instance.GetProperty("ContainerUser")?.EvaluatedValue;
        computedTag.Should().Be(expectedUser);
    }
}
