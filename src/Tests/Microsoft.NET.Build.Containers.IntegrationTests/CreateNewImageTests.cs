// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using FakeItEasy;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[Collection("Docker tests")]
public class CreateNewImageTests
{
    private ITestOutputHelper _testOutput;

    public CreateNewImageTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [DockerAvailableFact]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
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

        CreateNewImage task = new();

        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        task.BuildEngine = buildEngine;

        task.BaseRegistry = "mcr.microsoft.com";
        task.BaseImageName = "dotnet/runtime";
        task.BaseImageTag = "7.0";

        task.OutputRegistry = "localhost:5010";
        task.LocalRegistry = DockerAvailableFactAttribute.LocalRegistry;
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "linux-arm64", "publish");
        task.Repository = "dotnet/create-new-image-baseline";
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-arm64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(task.Execute(), FormatBuildMessages(errors));
        newProjectDir.Delete(true);
    }

    private static ImageConfig GetImageConfigFromTask(CreateNewImage task)
    {
        return new(task.GeneratedContainerConfiguration);
    }

    [DockerAvailableFact]
    public void ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());

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

        ParseContainerProperties pcp = new();
        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        pcp.BuildEngine = buildEngine;

        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:7.0";
        pcp.ContainerRegistry = "localhost:5010";
        pcp.ContainerRepository = "dotnet/testimage";
        pcp.ContainerImageTags = new[] { "5.0", "latest" };

        Assert.True(pcp.Execute(), FormatBuildMessages(errors));
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal("7.0", pcp.ParsedContainerTag);

        Assert.Equal("dotnet/testimage", pcp.NewContainerRepository);
        pcp.NewContainerTags.Should().BeEquivalentTo(new[] { "5.0", "latest" });

        CreateNewImage cni = new();
        (buildEngine, errors) = SetupBuildEngine();
        cni.BuildEngine = buildEngine;

        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.Repository = pcp.NewContainerRepository;
        cni.OutputRegistry = "localhost:5010";
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework);
        cni.WorkingDirectory = "app/";
        cni.Entrypoint = new TaskItem[] { new(newProjectDir.Name) };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(cni.Execute(), FormatBuildMessages(errors));
        newProjectDir.Delete(true);
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [DockerAvailableFact]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());

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

        ParseContainerProperties pcp = new();
        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        pcp.BuildEngine = buildEngine;

        pcp.FullyQualifiedBaseImageName = $"mcr.microsoft.com/{DockerRegistryManager.RuntimeBaseImage}:{DockerRegistryManager.Net8PreviewImageTag}";
        pcp.ContainerRegistry = "";
        pcp.ContainerRepository = "dotnet/envvarvalidation";
        pcp.ContainerImageTag = "latest";

        Dictionary<string, string> dict = new();
        dict.Add("Value", "Foo");

        pcp.ContainerEnvironmentVariables = new[] { new TaskItem("B@dEnv.Var", dict), new TaskItem("GoodEnvVar", dict) };

        Assert.True(pcp.Execute(), FormatBuildMessages(errors));
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal(DockerRegistryManager.Net8PreviewImageTag, pcp.ParsedContainerTag);
        Assert.Single(pcp.NewContainerEnvironmentVariables);
        Assert.Equal("Foo", pcp.NewContainerEnvironmentVariables[0].GetMetadata("Value"));

        Assert.Equal("dotnet/envvarvalidation", pcp.NewContainerRepository);
        Assert.Equal("latest", pcp.NewContainerTags[0]);

        CreateNewImage cni = new();
        (buildEngine, errors) = SetupBuildEngine();
        cni.BuildEngine = buildEngine;

        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.Repository = pcp.NewContainerRepository;
        cni.OutputRegistry = pcp.NewContainerRegistry;
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework, "linux-x64");
        cni.WorkingDirectory = "/app";
        cni.Entrypoint = new TaskItem[] { new($"/app/{newProjectDir.Name}") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();
        cni.LocalRegistry = DockerAvailableFactAttribute.LocalRegistry;

        Assert.True(cni.Execute(), FormatBuildMessages(errors));

        var config = GetImageConfigFromTask(cni);
        // because we're building off of .net 8 images for this test, we can validate the user id and aspnet https urls
        Assert.Equal("1654", config.GetUser());

        var ports = config.Ports;
        Assert.Single(ports);
        Assert.Equal(new(8080, PortType.tcp), ports.First());

        ContainerCli.RunCommand(_testOutput, "--rm", $"{pcp.NewContainerRepository}:latest")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Foo");
    }

    [DockerAvailableFact]
    public async System.Threading.Tasks.Task CreateNewImage_RootlessBaseImage()
    {
        const string RootlessBase = "dotnet/rootlessbase";
        const string AppImage = "dotnet/testimagerootless";
        const string RootlessUser = "1654";
        var loggerFactory = new TestLoggerFactory(_testOutput);
        var logger = loggerFactory.CreateLogger(nameof(CreateNewImage_RootlessBaseImage));

        // Build a rootless base runtime image.
        Registry registry = new(DockerRegistryManager.LocalRegistry, logger);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8PreviewImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);


        BuiltImage builtImage = imageBuilder.Build();

        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net8PreviewImageTag);
        var destinationReference = new DestinationImageReference(registry, RootlessBase, new[] { "latest" });

        await registry.PushAsync(builtImage, sourceReference, destinationReference, cancellationToken: default).ConfigureAwait(false);

        // Build an application image on top of the rootless base runtime image.
        DirectoryInfo newProjectDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_RootlessBaseImage)));

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

        new DotnetCommand(_testOutput, "publish", "-c", "Release", "-r", "linux-x64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage task = new();
        var (buildEngine, errors) = SetupBuildEngine();
        task.BuildEngine = buildEngine;
        task.BaseRegistry = "localhost:5010";
        task.BaseImageName = RootlessBase;
        task.BaseImageTag = "latest";

        task.OutputRegistry = "localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "linux-x64", "publish");
        task.Repository = AppImage;
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-x64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(task.Execute());
        newProjectDir.Delete(true);

        // Verify the application image uses the non-root user from the base image.
        imageBuilder = await registry.GetImageManifestAsync(
            AppImage,
            "latest",
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.Equal(RootlessUser, imageBuilder.BaseImageConfig.GetUser());
    }

    private static (IBuildEngine buildEngine, List<string?> errors) SetupBuildEngine()
    {
        List<string?> errors = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => errors.Add(e.Message));
        A.CallTo(() => buildEngine.LogErrorEvent(A<BuildErrorEventArgs>.Ignored)).Invokes((BuildErrorEventArgs e) => errors.Add(e.Message));
        A.CallTo(() => buildEngine.LogMessageEvent(A<BuildMessageEventArgs>.Ignored)).Invokes((BuildMessageEventArgs e) => errors.Add(e.Message));

        return (buildEngine, errors);
    }

    private static string GetTestDirectoryName([CallerMemberName] string testName = "DefaultTest") => Path.Combine(TestSettings.TestArtifactsDirectory, testName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));

    private static string FormatBuildMessages(List<string?> messages) => string.Join("\r\n", messages);
}
