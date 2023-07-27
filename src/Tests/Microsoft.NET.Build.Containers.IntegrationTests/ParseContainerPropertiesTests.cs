// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;
using static Microsoft.NET.Build.Containers.KnownStrings;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

public class ParseContainerPropertiesTests
{
    [Fact]
    public void Baseline()
    {
        var (project, _, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTags] = "7.0;latest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

        Assert.Equal("mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet/runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));

        Assert.Equal("dotnet/testimage", instance.GetPropertyValue(ContainerImageName));
        instance.GetItems(ContainerImageTags).Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "7.0", "latest" });
        instance.GetItems("ProjectCapability").Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "NetSdkOCIImageBuild" });
    }

    [Fact]
    public void SpacesGetReplacedWithDashes()
    {
         var (project, _, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr microsoft com/dotnet runtime:7.0",
            [ContainerRegistry] = "localhost:5010"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

        Assert.Equal("mcr-microsoft-com",instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));
    }

    [Fact]
    public void RegexCatchesInvalidContainerNames()
    {
         var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet testimage",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));
        Assert.Contains(logs.Messages, m => m.Message?.Contains("'ContainerImageName' was not a valid container image name, it was normalized to 'dotnet-testimage'") == true);
    }

    [Fact]
    public void RegexCatchesInvalidContainerTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTag] = "5 0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2007);
    }

    [Fact]
    public void CanOnlySupplyOneOfTagAndTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTag] = "5.0",
            [ContainerImageTags] = "latest;oldest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2008);
    }

    [Fact]
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
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER2014);
    }
}
