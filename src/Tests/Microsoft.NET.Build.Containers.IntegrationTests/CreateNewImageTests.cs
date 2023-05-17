// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[Collection("Docker tests")]
public class CreateNewImageTests
{
    private ITestOutputHelper _testOutput;

    public CreateNewImageTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [DockerDaemonAvailableFact]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_Baseline)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "publish", "-c", "Release", "-r", "linux-arm64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage task = new CreateNewImage();
        task.BaseRegistry = "mcr.microsoft.com";
        task.BaseImageName = "dotnet/runtime";
        task.BaseImageTag = "7.0";

        task.OutputRegistry = "localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "linux-arm64", "publish");
        task.ImageName = "dotnet/testimage";
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-arm64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(task.Execute());
        newProjectDir.Delete(true);
    }

    [DockerDaemonAvailableFact]
    public void ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(ParseContainerProperties_EndToEnd)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "build", "--configuration", "release")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:7.0";
        pcp.ContainerRegistry = "localhost:5010";
        pcp.ContainerImageName = "dotnet/testimage";
        pcp.ContainerImageTags = new[] { "5.0", "latest" };

        Assert.True(pcp.Execute());
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal("7.0", pcp.ParsedContainerTag);

        Assert.Equal("dotnet/testimage", pcp.NewContainerImageName);
        pcp.NewContainerTags.Should().BeEquivalentTo(new[] { "5.0", "latest" });

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = "localhost:5010";
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework);
        cni.WorkingDirectory = "app/";
        cni.Entrypoint = new TaskItem[] { new("ParseContainerProperties_EndToEnd") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(cni.Execute());
        newProjectDir.Delete(true);
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [DockerDaemonAvailableFact]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(Tasks_EndToEnd_With_EnvironmentVariable_Validation)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"), $"Console.Write(Environment.GetEnvironmentVariable(\"GoodEnvVar\"));");

        new DotnetCommand(_testOutput, "build", "--configuration", "release", "/p:runtimeidentifier=linux-x64", $"/p:RuntimeFrameworkVersion=8.0.0-preview.3.23174.8")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = $"mcr.microsoft.com/{DockerRegistryManager.RuntimeBaseImage}:{DockerRegistryManager.Net8PreviewImageTag}";
        pcp.ContainerRegistry = "";
        pcp.ContainerImageName = "dotnet/envvarvalidation";
        pcp.ContainerImageTag = "latest";

        Dictionary<string, string> dict = new Dictionary<string, string>();
        dict.Add("Value", "Foo");

        pcp.ContainerEnvironmentVariables = new[] { new TaskItem("B@dEnv.Var", dict), new TaskItem("GoodEnvVar", dict) };

        Assert.True(pcp.Execute());
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal(DockerRegistryManager.Net8PreviewImageTag, pcp.ParsedContainerTag);
        Assert.Single(pcp.NewContainerEnvironmentVariables);
        Assert.Equal("Foo", pcp.NewContainerEnvironmentVariables[0].GetMetadata("Value"));

        Assert.Equal("dotnet/envvarvalidation", pcp.NewContainerImageName);
        Assert.Equal("latest", pcp.NewContainerTags[0]);

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = pcp.NewContainerRegistry;
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework, "linux-x64");
        cni.WorkingDirectory = "/app";
        cni.Entrypoint = new TaskItem[] { new("/app/Tasks_EndToEnd_With_EnvironmentVariable_Validation") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();
        cni.LocalContainerDaemon = global::Microsoft.NET.Build.Containers.KnownDaemonTypes.Docker;

        Assert.True(cni.Execute());

        new RunExeCommand(_testOutput, "docker", "run", "--rm", $"{pcp.NewContainerImageName}:latest")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Foo");
    }
}
