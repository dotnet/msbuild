// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerRegistryManager
{
    public const string RuntimeBaseImage = "dotnet/runtime";
    public const string AspNetBaseImage = "dotnet/aspnet";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string Net6ImageTag = "6.0";
    public const string Net7ImageTag = "7.0";
    public const string Net8PreviewImageTag = "8.0-preview";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}{RuntimeBaseImage}:{Net8PreviewImageTag}";
    public const string FullyQualifiedBaseImageAspNet = $"{BaseImageSource}{AspNetBaseImage}:{Net8PreviewImageTag}";
    private static string? s_registryContainerId;

    public static void StartAndPopulateDockerRegistry(ITestOutputHelper testOutput)
    {
        testOutput.WriteLine("Spawning local registry");
        if (!new LocalDocker(testOutput.WriteLine).IsAvailable()) {
            throw new InvalidOperationException("Docker daemon is not started, tests cannot run");
        }
        CommandResult processResult = new RunExeCommand(testOutput, "docker", "run", "--rm", "--publish", "5010:5000", "--detach", "registry:2").Execute();
        processResult.Should().Pass().And.HaveStdOut();
        using var reader = new StringReader(processResult.StdOut!);
        s_registryContainerId = reader.ReadLine();

        foreach (var tag in new[] { Net6ImageTag, Net7ImageTag, Net8PreviewImageTag })
        {
            new RunExeCommand(testOutput, "docker", "pull", $"{BaseImageSource}{RuntimeBaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new RunExeCommand(testOutput, "docker", "tag", $"{BaseImageSource}{RuntimeBaseImage}:{tag}", $"{LocalRegistry}/{RuntimeBaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new RunExeCommand(testOutput, "docker", "push", $"{LocalRegistry}/{RuntimeBaseImage}:{tag}")
                .Execute()
                .Should().Pass();
        }
    }

    public static void ShutdownDockerRegistry(ITestOutputHelper testOutput)
    {
        if (s_registryContainerId != null)
        {
            new RunExeCommand(testOutput, "docker", "stop", s_registryContainerId)
                .Execute()
                .Should().Pass();
        }
    }
}
