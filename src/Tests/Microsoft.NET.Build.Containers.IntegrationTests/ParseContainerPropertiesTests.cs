// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;
using static Microsoft.NET.Build.Containers.KnownStrings;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[Collection("Docker tests")]
public class ParseContainerPropertiesTests
{
    [DockerDaemonAvailableFact]
    public void Baseline()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTags] = "7.0;latest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));

        Assert.Equal("mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet/runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));

        Assert.Equal("dotnet/testimage", instance.GetPropertyValue(ContainerRepository));
        instance.GetItems(ContainerImageTags).Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "7.0", "latest" });
        instance.GetItems("ProjectCapability").Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "NetSdkOCIImageBuild" });
    }

    [DockerDaemonAvailableFact]
    public void SpacesGetReplacedWithDashes()
    {
         var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr microsoft com/dotnet runtime:7.0",
            [ContainerRegistry] = "localhost:5010"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));

        Assert.Equal("mcr-microsoft-com",instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));
    }

    [DockerDaemonAvailableFact]
    public void RegexCatchesInvalidContainerNames()
    {
         var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet testimage",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));
        Assert.Contains(logs.Messages, m => m.Message?.Contains("'dotnet testimage' was not a valid container image name, it was normalized to 'dotnet-testimage'") == true);
    }

    [DockerDaemonAvailableFact]
    public void RegexCatchesInvalidContainerTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTag] = "5 0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2007);
    }

    [DockerDaemonAvailableFact]
    public void CanOnlySupplyOneOfTagAndTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTag] = "5.0",
            [ContainerImageTags] = "latest;oldest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2008);
    }

    [DockerDaemonAvailableFact]
    public void FailsOnCompletelyInvalidRepositoryNames()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "㓳㓴㓵㓶㓷㓹㓺㓻",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2005);
    }

    [DockerDaemonAvailableFact]
    public void FailsWhenFirstCharIsAUnicodeLetterButNonLatin()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "㓳but-otherwise-valid",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2005);
    }
}
