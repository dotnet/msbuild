// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class EndToEndTests : IDisposable
{
    private ITestOutputHelper _testOutput;
    private readonly TestLoggerFactory _loggerFactory;

    public EndToEndTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _loggerFactory = new TestLoggerFactory(testOutput);
    }

    public static string NewImageName([CallerMemberName] string callerMemberName = "")
    {
        var (normalizedName, warning, error) = ContainerHelpers.NormalizeRepository(callerMemberName);
        if (error is (var format, var args))
        {
            throw new ArgumentException(String.Format(Strings.ResourceManager.GetString(format)!, args));
        }

        return normalizedName!; // non-null if error is null
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [DockerDaemonAvailableFact]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithRegistryPushAndPull));
        string publishDirectory = BuildLocalApp();

        // Build the image

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry), logger);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.BaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntryPoint(new[] { "/app/MinimalTestApp" });

        BuiltImage builtImage = imageBuilder.Build();

        // Push the image back to the local registry
        var sourceReference = new ImageReference(registry, DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), "latest");

        await registry.PushAsync(builtImage, sourceReference, destinationReference, cancellationToken: default).ConfigureAwait(false);

        // pull it back locally
        new RunExeCommand(_testOutput, "docker", "pull", $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest")
            .Execute()
            .Should().Pass();

        // Run the image
        new RunExeCommand(_testOutput, "docker", "run", "--rm", "--tty", $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest")
            .Execute()
            .Should().Pass();
    }

    [DockerDaemonAvailableFact]
    public async Task ApiEndToEndWithLocalLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithLocalLoad));
        string publishDirectory = BuildLocalApp();

        // Build the image

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry), logger);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.BaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntryPoint(new[] { "/app/MinimalTestApp" });

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local Docker daemon
        var sourceReference = new ImageReference(registry, DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), "latest");

        await new LocalDocker(logger).LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        // Run the image
        new RunExeCommand(_testOutput, "docker", "run", "--rm", "--tty", $"{NewImageName()}:latest")
            .Execute()
            .Should().Pass();
    }

    private string BuildLocalApp([CallerMemberName] string testName = "TestName", string tfm = ToolsetInfo.CurrentTargetFramework, string rid = "linux-x64")
    {
        string workingDirectory = Path.Combine(TestSettings.TestArtifactsDirectory, testName);

        DirectoryInfo d = new DirectoryInfo(Path.Combine(workingDirectory, "MinimalTestApp"));
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }
        Directory.CreateDirectory(workingDirectory);

        new DotnetNewCommand(_testOutput, "console", "-f", tfm, "-o", "MinimalTestApp")
            .WithVirtualHive()        
            .WithWorkingDirectory(workingDirectory)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "publish", "-bl", "MinimalTestApp", "-r", rid, "-f", tfm, "-c", "Debug")
            .WithWorkingDirectory(workingDirectory)
            .Execute()
            .Should().Pass();

        string publishDirectory = Path.Join(workingDirectory, "MinimalTestApp", "bin", "Debug", tfm, rid, "publish");
        return publishDirectory;
    }

    [DockerDaemonAvailableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndToEnd_NoAPI_Web(bool addPackageReference)
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, $"CreateNewImageTest_{addPackageReference}"));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        newProjectDir.Create();
        privateNuGetAssets.Create();

        new DotnetNewCommand(_testOutput, "webapi", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()  
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        if (addPackageReference)
        {
            File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

            (string packagePath, string packageVersion) = ToolsetUtils.GetContainersPackagePath();

            new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath), "--name", "local-temp")
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();

            // Add package to the project
            new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", packageVersion)
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();
        }
        else
        {
            string projectPath = Path.Combine(newProjectDir.FullName, newProjectDir.Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

            project.Root?.Add(new XElement("PropertyGroup", new XElement("EnableSDKContainerSupport", "true")));
            project.Save(projectPath);
        }

        string imageName = NewImageName();
        string imageTag = "1.0";

        // Build & publish the project
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/p:publishprofile=DefaultContainer",
            "/p:runtimeidentifier=linux-x64",
            "/bl",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageDefault}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        commandResult.Should().Pass();

        if (addPackageReference)
        {
            commandResult.Should().HaveStdOutContaining("warning : Microsoft.NET.Build.Containers NuGet package is explicitly referenced. Consider removing the package reference to Microsoft.NET.Build.Containers as it is now part of .NET SDK.");
        }
        else
        {
            commandResult.Should().NotHaveStdOutContaining("warning");
        }

        new RunExeCommand(_testOutput, "docker", "pull", $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
            .Execute()
            .Should().Pass();

        var containerName = "test-container-1";
        CommandResult processResult = new RunExeCommand(
            _testOutput,
            "docker",
            "run",
            "--rm",
            "--name",
            containerName,
            "--publish",
            "5017:80",
            "--detach",
            $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
        .Execute();
        processResult.Should().Pass();
        Assert.NotNull(processResult.StdOut);

        string appContainerId = processResult.StdOut.Trim();

        bool everSucceeded = false;

        HttpClient client = new();

        // Give the server a moment to catch up, but no more than necessary.
        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                var response = await client.GetAsync("http://localhost:5017/weatherforecast").ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    everSucceeded = true;
                    break;
                }
            }
            catch { }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        new RunExeCommand(_testOutput, "docker", "logs", appContainerId)
            .Execute()
            .Should().Pass();

        Assert.True(everSucceeded, "http://localhost:5017/weatherforecast never responded.");

        new RunExeCommand(_testOutput, "docker", "stop", appContainerId)
            .Execute()
            .Should().Pass();

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerDaemonAvailableFact]
    public void EndToEnd_NoAPI_Console()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "CreateNewImageTest"));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        newProjectDir.Create();
        privateNuGetAssets.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

        (string packagePath, string packageVersion) = ToolsetUtils.GetContainersPackagePath();

        new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath), "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the project
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", packageVersion)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        string imageName = NewImageName();
        string imageTag = "1.0";

        // Build & publish the project
        new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            "/p:runtimeidentifier=linux-x64",
            "/bl",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageDefault}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new RunExeCommand(_testOutput, "docker", "pull", $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
            .Execute()
            .Should().Pass();

        var containerName = "test-container-2";
        CommandResult processResult = new RunExeCommand(
            _testOutput,
            "docker",
            "run",
            "--rm",
            "--name",
            containerName,
            $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
        .Execute();
        processResult.Should().Pass().And.HaveStdOut("Hello, World!");

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerSupportsArchInlineData("linux/arm/v7", "linux-arm", "/app")]
    [DockerSupportsArchInlineData("linux/arm64/v8", "linux-arm64", "/app")]
    [DockerSupportsArchInlineData("linux/386", "linux-x86", "/app", Skip="There's no apphost for linux-x86 so we can't execute self-contained, and there's no .NET runtime base image for linux-x86 so we can't execute framework-dependent.")]
    [DockerSupportsArchInlineData("windows/amd64", "win-x64", "C:\\app")]
    [DockerSupportsArchInlineData("linux/amd64", "linux-x64", "/app")]
    [DockerDaemonAvailableTheory]
    public async Task CanPackageForAllSupportedContainerRIDs(string dockerPlatform, string rid, string workingDir)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanPackageForAllSupportedContainerRIDs));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework, rid: rid);

        // Build the image
        Registry registry = new(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.BaseImageSource), logger);

        ImageBuilder? imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.BaseImage,
            DockerRegistryManager.Net7ImageTag,
            rid,
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false);

        imageBuilder.AddLayer(l);
        imageBuilder.SetWorkingDirectory(workingDir);

        string[] entryPoint = DecideEntrypoint(rid, "MinimalTestApp", workingDir);
        imageBuilder.SetEntryPoint(entryPoint);

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local Docker daemon
        var sourceReference = new ImageReference(registry, DockerRegistryManager.BaseImage, DockerRegistryManager.Net7ImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), rid);
        await new LocalDocker(logger).LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        // Run the image
        new RunExeCommand(
            _testOutput,
            "docker",
            "run",
            "--rm",
            "--tty",
            "--platform",
            dockerPlatform,
            $"{NewImageName()}:{rid}")
            .Execute()
            .Should()
            .Pass();

        static string[] DecideEntrypoint(string rid, string appName, string workingDir)
        {
            var binary = rid.StartsWith("win", StringComparison.Ordinal) ? $"{appName}.exe" : appName;
            return new[] { $"{workingDir}/{binary}" };
        }
    }
}
