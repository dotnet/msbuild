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
        var (project, _) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTags] = "7.0;latest"
        });
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

        Assert.Equal("mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet/runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));

        Assert.Equal("dotnet/testimage", instance.GetPropertyValue(ContainerImageName));
        instance.GetItems(ContainerImageTags).Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "7.0", "latest" });
        instance.GetItems("ProjectCapability").Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "NetSdkOCIImageBuild" });
    }

    [DockerDaemonAvailableFact]
    public void SpacesGetReplacedWithDashes()
    {
         var (project, _) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr microsoft com/dotnet runtime:7.0",
            [ContainerRegistry] = "localhost:5010"
        });

        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

        Assert.Equal("mcr-microsoft-com",instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.Equal("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.Equal("7.0", instance.GetPropertyValue(ContainerBaseTag));
    }

    [DockerDaemonAvailableFact]
    public void RegexCatchesInvalidContainerNames()
    {
         var (project, logs) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet testimage",
            [ContainerImageTag] = "5.0"
        });

        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.True(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));
        Assert.Contains(logs.Messages, m => m.Code == ErrorCodes.CONTAINER001 && m.Importance == global::Microsoft.Build.Framework.MessageImportance.High);
    }

    [DockerDaemonAvailableFact]
    public void RegexCatchesInvalidContainerTags()
    {
        var (project, logs) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTag] = "5 0"
        });

        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER004);
    }

    [DockerDaemonAvailableFact]
    public void CanOnlySupplyOneOfTagAndTags()
    {
        var (project, logs) = ProjectInitializer.InitProject(new () {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "dotnet/testimage",
            [ContainerImageTag] = "5.0",
            [ContainerImageTags] = "latest;oldest"
        });

        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.False(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

        Assert.True(logs.Errors.Count > 0);
        Assert.Equal(logs.Errors[0].Code, ErrorCodes.CONTAINER005);
    }
}
