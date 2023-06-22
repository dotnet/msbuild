// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class RegistryTests
{
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
