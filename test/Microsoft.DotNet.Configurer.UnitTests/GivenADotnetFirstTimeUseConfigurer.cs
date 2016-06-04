// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenADotnetFirstTimeUseConfigurer
    {
        private const string NUGET_CACHE_PATH = "some path";

        private Mock<INuGetCachePrimer> _nugetCachePrimerMock;
        private Mock<INuGetCacheResolver> _nugetCacheResolverMock;

        public GivenADotnetFirstTimeUseConfigurer()
        {
            _nugetCachePrimerMock = new Mock<INuGetCachePrimer>();
            _nugetCacheResolverMock = new Mock<INuGetCacheResolver>();
            _nugetCacheResolverMock.Setup(n => n.ResolveNugetCachePath()).Returns(NUGET_CACHE_PATH);
        }

        [Fact]
        public void The_sentinel_has_the_current_version_in_its_name()
        {
            DotnetFirstTimeUseConfigurer.SENTINEL.Should().Contain($"{Product.Version}");
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            fileSystemMockBuilder.AddFiles(NUGET_CACHE_PATH, DotnetFirstTimeUseConfigurer.SENTINEL);

            var fileSystemMock = fileSystemMockBuilder.Build();

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheResolverMock.Object,
                fileSystemMock.File);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_primes_the_cache_if_the_sentinel_does_not_exist()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            var fileSystemMock = fileSystemMockBuilder.Build();

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheResolverMock.Object,
                fileSystemMock.File);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Once);
        }
    }
}
