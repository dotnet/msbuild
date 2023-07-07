// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class RegistryTests : IDisposable
{
    private ITestOutputHelper _testOutput;
    private readonly TestLoggerFactory _loggerFactory;

    public RegistryTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _loggerFactory = new TestLoggerFactory(testOutput);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [DockerDaemonAvailableFact]
    public async Task GetFromRegistry()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(GetFromRegistry));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry), logger);
        var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();

        // Don't need rid graph for local registry image pulls - since we're only pushing single image manifests (not manifest lists)
        // as part of our setup, we could put literally anything in here. The file at the passed-in path would only get read when parsing manifests lists.
        ImageBuilder? downloadedImage = await registry.GetImageManifestAsync(
            DockerRegistryManager.BaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ridgraphfile,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(downloadedImage);
    }

    [InlineData("quay.io/centos/centos")]
    [InlineData("registry.access.redhat.com/ubi8/dotnet-70")]
    [DockerDaemonAvailableTheory]
    public async Task CanReadManifestFromRegistry(string fullyQualifiedContainerName)
    {
        bool parsed = ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedContainerName,
                                                                           out string? containerRegistry,
                                                                           out string? containerName,
                                                                           out string? containerTag,
                                                                           out string? containerDigest);
        Assert.True(parsed);
        Assert.NotNull(containerRegistry);
        Assert.NotNull(containerName);
        containerTag ??= "latest";

        ILogger logger = _loggerFactory.CreateLogger(nameof(CanReadManifestFromRegistry));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(containerRegistry), logger);

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
