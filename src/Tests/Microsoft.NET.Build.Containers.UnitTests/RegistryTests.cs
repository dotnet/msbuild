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

    [InlineData("public.ecr.aws", true)]
    [InlineData("123412341234.dkr.ecr.us-west-2.amazonaws.com", true)]
    [InlineData("123412341234.dkr.ecr-fips.us-west-2.amazonaws.com", true)]
    [InlineData("notvalid.dkr.ecr.us-west-2.amazonaws.com", false)]
    [InlineData("1111.dkr.ecr.us-west-2.amazonaws.com", false)]
    [InlineData("mcr.microsoft.com", false)]
    [InlineData("localhost", false)]
    [InlineData("hub", false)]
    [Theory]
    public void CheckIfAmazonECR(string registryName, bool isECR)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfAmazonECR));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
        Assert.Equal(isECR, registry.IsAmazonECRRegistry);
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
}
