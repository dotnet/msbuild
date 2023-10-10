// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerAvailableTheoryAttribute : TheoryAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

    public DockerAvailableTheoryAttribute(bool skipPodman = false)
    {
        if (!DockerCliStatus.IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }

        if (skipPodman && DockerCliStatus.Command == DockerCli.PodmanCommand)
        {
            base.Skip = $"Skipping test with {DockerCliStatus.Command} cli.";
        }
    }
}

public class DockerAvailableFactAttribute : FactAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

    public DockerAvailableFactAttribute(bool skipPodman = false)
    {
        if (!DockerCliStatus.IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }

        if (skipPodman && DockerCliStatus.Command == DockerCli.PodmanCommand)
        {
            base.Skip = $"Skipping test with {DockerCliStatus.Command} cli.";
        }
    }
}

// tiny optimization - since there are many instances of this attribute we should only get
// the daemon status once
static file class DockerCliStatus
{
    public static readonly bool IsAvailable;
    public static readonly string? Command;
    public static string LocalRegistry
        => Command == DockerCli.PodmanCommand ? KnownLocalRegistryTypes.Podman
                                              : KnownLocalRegistryTypes.Docker;

    static DockerCliStatus()
    {
        DockerCli cli = new(new TestLoggerFactory());
        IsAvailable = cli.IsAvailable();
        Command = cli.GetCommand();
    }
}
