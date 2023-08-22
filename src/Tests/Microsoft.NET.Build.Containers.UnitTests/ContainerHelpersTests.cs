// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ContainerHelpersTests
{
    private const string DefaultRegistry = "docker.io";

    [Theory]
    // Valid Tests
    [InlineData("mcr.microsoft.com", true)]
    [InlineData("mcr.microsoft.com:5001", true)] // Registries can have ports
    [InlineData("docker.io", true)] // default docker registry is considered valid

    // // Invalid tests
    [InlineData("mcr.mi-=crosoft.com", false)] // invalid url
    [InlineData("mcr.microsoft.com/", false)] // invalid url
    public void IsValidRegistry(string registry, bool expectedReturn)
    {
        Console.WriteLine($"Domain pattern is '{ReferenceParser.AnchoredDomainRegexp.ToString()}'");
        Assert.Equal(expectedReturn, ContainerHelpers.IsValidRegistry(registry));
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/runtime:6.0", true, "mcr.microsoft.com", "dotnet/runtime", "6.0", true)]
    [InlineData("mcr.microsoft.com/dotnet/runtime", true, "mcr.microsoft.com", "dotnet/runtime", null, true)]
    [InlineData("mcr.microsoft.com/", false, null, null, null, false)] // no image = nothing resolves
    // Ports tag along
    [InlineData("mcr.microsoft.com:54/dotnet/runtime", true, "mcr.microsoft.com:54", "dotnet/runtime", null, true)]
    // Even if nonsensical
    [InlineData("mcr.microsoft.com:0/dotnet/runtime", true, "mcr.microsoft.com:0", "dotnet/runtime", null, true)]
    // We don't allow hosts with missing ports when a port is anticipated
    [InlineData("mcr.microsoft.com:/dotnet/runtime", false, null, null, null, false)]
    // Use default registry when no registry specified.
    [InlineData("ubuntu:jammy", true, DefaultRegistry, "library/ubuntu", "jammy", false)]
    [InlineData("ubuntu/runtime:jammy", true, DefaultRegistry, "ubuntu/runtime", "jammy", false)]
    // Alias 'docker.io' to Docker registry.
    [InlineData("docker.io/ubuntu:jammy", true, DefaultRegistry, "library/ubuntu", "jammy", true)]
    [InlineData("docker.io/ubuntu/runtime:jammy", true, DefaultRegistry, "ubuntu/runtime", "jammy", true)]
    // 'localhost' registry.
    [InlineData("localhost/ubuntu:jammy", true, "localhost", "ubuntu", "jammy", true)]
    public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string expectedRegistry, string expectedImage, string expectedTag, bool expectedIsRegistrySpecified)
    {
        Assert.Equal(expectedReturn, ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string? containerReg, out string? containerName, out string? containerTag, out string? containerDigest, out bool isRegistrySpecified));
        Assert.Equal(expectedRegistry, containerReg);
        Assert.Equal(expectedImage, containerName);
        Assert.Equal(expectedTag, containerTag);
        Assert.Equal(expectedIsRegistrySpecified, isRegistrySpecified);
    }

    [Theory]
    [InlineData("dotnet/runtime", true)]
    [InlineData("foo/bar", true)]
    [InlineData("registry", true)]
    [InlineData("-foo/bar", false)]
    [InlineData(".foo/bar", false)]
    [InlineData("_foo/bar", false)]
    [InlineData("foo/bar-", false)]
    [InlineData("foo/bar.", false)]
    [InlineData("foo/bar_", false)]
    public void IsValidImageName(string imageName, bool expectedReturn)
    {
        Assert.Equal(expectedReturn, ContainerHelpers.IsValidImageName(imageName));
    }

    [Theory]
    [InlineData("6.0", true)] // baseline
    [InlineData("5.2-asd123", true)] // with commit hash
    [InlineData(".6.0", false)] // starts with .
    [InlineData("-6.0", false)] // starts with -
    [InlineData("---", false)] // malformed
    public void IsValidImageTag(string imageTag, bool expectedReturn)
    {
        Assert.Equal(expectedReturn, ContainerHelpers.IsValidImageTag(imageTag));
    }

    [Fact]
    public void IsValidImageTag_InvalidLength()
    {
        Assert.False(ContainerHelpers.IsValidImageTag(new string('a', 129)));
    }

    [Theory]
    [InlineData("80/tcp", true, 80, PortType.tcp, null)]
    [InlineData("80", true, 80, PortType.tcp, null)]
    [InlineData("125/dup", false, 125, PortType.tcp, ContainerHelpers.ParsePortError.InvalidPortType)]
    [InlineData("invalidNumber", false, null, null, ContainerHelpers.ParsePortError.InvalidPortNumber)]
    [InlineData("welp/unknowntype", false, null, null, (ContainerHelpers.ParsePortError)3)]
    [InlineData("a/b/c", false, null, null, ContainerHelpers.ParsePortError.UnknownPortFormat)]
    [InlineData("/tcp", false, null, null, ContainerHelpers.ParsePortError.MissingPortNumber)]
    public void CanParsePort(string input, bool shouldParse, int? expectedPortNumber, PortType? expectedType, ContainerHelpers.ParsePortError? expectedError)
    {
        var parseSuccess = ContainerHelpers.TryParsePort(input, out var port, out var errors);
        Assert.Equal(shouldParse, parseSuccess);

        if (shouldParse)
        {
            Assert.NotNull(port);
            Assert.Equal(port.Value.Number, expectedPortNumber);
            Assert.Equal(port.Value.Type, expectedType);
        }
        else
        {
            Assert.Null(port);
            Assert.NotNull(errors);
            Assert.Equal(expectedError, errors);
        }
    }

    [Theory]
    [InlineData("FOO", true)]
    [InlineData("foo_bar", true)]
    [InlineData("foo-bar", false)]
    [InlineData("foo.bar", false)]
    [InlineData("foo bar", false)]
    [InlineData("1_NAME", false)]
    [InlineData("ASPNETCORE_URLS", true)]
    [InlineData("ASPNETCORE_URLS2", true)]
    public void CanRecognizeEnvironmentVariableNames(string envVarName, bool isValid)
    {
        var success = ContainerHelpers.IsValidEnvironmentVariable(envVarName);
        Assert.Equal(isValid, success);
    }
}
