// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests;

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
        
    [InlineData("us-south1-docker.pkg.dev", true)]
    [InlineData("us.gcr.io", false)]
    [Theory]
    public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfGoogleArtifactRegistry));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
        Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
    }

    [Fact]
    public void DockerIoAlias()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(DockerIoAlias));
        Registry registry = new Registry(new Uri("https://docker.io"), logger);
        Assert.True(registry.IsDockerHub);
        Assert.Equal("docker.io", registry.RegistryName);
        Assert.Equal("registry-1.docker.io", registry.BaseUri.Host);
    }
}
