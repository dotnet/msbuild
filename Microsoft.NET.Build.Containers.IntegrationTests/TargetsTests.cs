// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static Microsoft.NET.Build.Containers.KnownStrings.Properties;
using FluentAssertions;
using Microsoft.Build.Execution;
using Xunit;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;

namespace Microsoft.NET.Build.Containers.Targets.IntegrationTests;

[Collection("Docker tests")]
public class TargetsTests
{
    [InlineData(true, "/app/foo.exe")]
    [InlineData(false, "dotnet", "/app/foo.dll")]
    [DockerDaemonAvailableTheory]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [UseAppHost] = useAppHost.ToString()
        }, projectName: $"{nameof(CanSetEntrypointArgsToUseAppHost)}_{useAppHost}_{String.Join("_", entrypointArgs)}");
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
    [DockerDaemonAvailableTheory]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [AssemblyName] = projectName
        }, projectName: $"{nameof(CanNormalizeInputContainerNames)}_{projectName}_{expectedContainerImageName}_{shouldPass}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().Be(shouldPass, "Build should have succeeded");
        Assert.Equal(expectedContainerImageName, instance.GetPropertyValue(ContainerImageName));
    }

    [InlineData("7.0.100", true)]
    [InlineData("8.0.100", true)]
    [InlineData("7.0.100-preview.7", true)]
    [InlineData("7.0.100-rc.1", true)]
    [InlineData("6.0.100", false)]
    [InlineData("7.0.100-preview.1", false)]
    [DockerDaemonAvailableTheory]
    public void CanWarnOnInvalidSDKVersions(string sdkVersion, bool isAllowed)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanWarnOnInvalidSDKVersions)}_{sdkVersion}_{isAllowed}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        var derivedIsAllowed = Boolean.Parse(project.GetProperty("_IsSDKContainerAllowedVersion").EvaluatedValue);
        // var buildResult = instance.Build(new[]{"_ContainerVerifySDKVersion"}, null, null, out var outputs);
        derivedIsAllowed.Should().Be(isAllowed, $"SDK version {(isAllowed ? "should" : "should not")} have been allowed ");
    }

    [InlineData(true)]
    [InlineData(false)]
    [DockerDaemonAvailableTheory]
    public void GetsConventionalLabelsByDefault(bool shouldEvaluateLabels)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [ContainerGenerateLabels] = shouldEvaluateLabels.ToString()
        }, projectName: $"{nameof(GetsConventionalLabelsByDefault)}_{shouldEvaluateLabels}");
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
    [DockerDaemonAvailableTheory]
    public void ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn(bool includeSourceControl)
    {
        var commitHash = "abcdef";
        var repoUrl = "https://git.cosmere.com/shard/whimsy.git";

        var (project, _) = ProjectInitializer.InitProject(new()
        {
            ["PublishRepositoryUrl"] = includeSourceControl.ToString(),
            ["PrivateRepositoryUrl"] = repoUrl,
            ["SourceRevisionId"] = commitHash
        }, projectName: $"{nameof(ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn)}_{includeSourceControl}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue("Build should have succeeded");
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

}
