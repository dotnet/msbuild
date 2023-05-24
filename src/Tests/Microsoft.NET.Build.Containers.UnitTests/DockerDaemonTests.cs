// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

[CollectionDefinition("Daemon Tests")]
public class DaemonTestsCollection
{ 
}

[Collection("Daemon Tests")]
public class DockerDaemonTests
{
    [DockerAvailableFact(skipPodman: true)] // podman is a local cli not meant for connecting to remote Docker daemons.
    public async Task Can_detect_when_no_daemon_is_running() {
        // mimic no daemon running by setting the DOCKER_HOST to a nonexistent socket
        try {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://123.123.123.123:12345");
            var available = await new DockerCli(Console.WriteLine).IsAvailableAsync(default).ConfigureAwait(false);
            Assert.False(available, "No daemon should be listening at that port");
        } finally {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [DockerAvailableFact]
    public async Task Can_detect_when_daemon_is_running() {
        var available = await new DockerCli(Console.WriteLine).IsAvailableAsync(default).ConfigureAwait(false);
        Assert.True(available, "Should have found a working daemon");
    }
}
