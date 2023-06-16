// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class RegistryTests
{
    [DockerAvailableFact]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));
        var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();

        // Don't need rid graph for local registry image pulls - since we're only pushing single image manifests (not manifest lists)
        // as part of our setup, we could put literally anything in here. The file at the passed-in path would only get read when parsing manifests lists.
        ImageBuilder? downloadedImage = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ridgraphfile,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(downloadedImage);
    }

    [InlineData("quay.io/centos/centos")]
    [InlineData("registry.access.redhat.com/ubi8/dotnet-70")]
    [Theory]
    public async Task CanReadManifestFromRegistry(string fullyQualifiedContainerName)
    {
        bool parsed = ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedContainerName,
                                                                           out string? containerRegistry,
                                                                           out string? containerName,
                                                                           out string? containerTag,
                                                                           out string? containerDigest,
                                                                           out bool isRegistrySpecified);
        Assert.True(parsed);
        Assert.True(isRegistrySpecified);
        Assert.NotNull(containerRegistry);
        Assert.NotNull(containerName);
        containerTag ??= "latest";

        Registry registry = new Registry(new Uri($"https://{containerRegistry}"));

        var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();

        ImageBuilder? downloadedImage = await registry.GetImageManifestAsync(
            containerName,
            containerTag,
            "linux-x64",
            ridgraphfile,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(downloadedImage);
    }
}
