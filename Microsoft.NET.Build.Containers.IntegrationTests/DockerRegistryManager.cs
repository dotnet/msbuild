// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.CommandUtils;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string Net6ImageTag = "6.0";
    public const string Net7ImageTag = "7.0";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}{BaseImage}:{Net6ImageTag}";
    private static string? s_registryContainerId;

    public static void StartAndPopulateDockerRegistry(ITestOutputHelper testOutput)
    {
        testOutput.WriteLine("Spawning local registry");
        CommandResult processResult = new BasicCommand(testOutput, "docker", "run", "--rm", "--publish", "5010:5000", "--detach", "registry:2").Execute();
        processResult.Should().Pass().And.HaveStdOut();
        using var reader = new StringReader(processResult.StdOut!);
        s_registryContainerId = reader.ReadLine();

        foreach (var tag in new[] { Net6ImageTag, Net7ImageTag })
        {
            new BasicCommand(testOutput, "docker", "pull", $"{BaseImageSource}{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new BasicCommand(testOutput, "docker", "tag", $"{BaseImageSource}{BaseImage}:{tag}", $"{LocalRegistry}/{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new BasicCommand(testOutput, "docker", "push", $"{LocalRegistry}/{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();
        }
    }

    public static void ShutdownDockerRegistry(ITestOutputHelper testOutput)
    {
        if (s_registryContainerId != null)
        {
            new BasicCommand(testOutput, "docker", "stop", s_registryContainerId)
                .Execute()
                .Should().Pass();
        }
    }
}
