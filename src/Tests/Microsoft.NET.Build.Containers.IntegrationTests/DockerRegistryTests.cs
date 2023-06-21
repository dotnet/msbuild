// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class DockerRegistryTests
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
}
