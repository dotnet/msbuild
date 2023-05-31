// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class RegistryTests
    {
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
            Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
            Assert.Equal(isECR, registry.IsAmazonECRRegistry);
        }

        [InlineData("us-south1-docker.pkg.dev", true)]
        [InlineData("us.gcr.io", false)]
        [Theory]
        public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
        {
            Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
            Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
        }

        [Fact]
        public void DockerIoAlias()
        {
            Registry registry = new Registry(new Uri("https://docker.io"));
            Assert.True(registry.IsDockerHub);
            Assert.Equal("docker.io", registry.RegistryName);
            Assert.Equal("registry-1.docker.io", registry.BaseUri.Host);
        }
    }
}
