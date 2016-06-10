// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.DependencyModel.Tests;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenADotnetFirstTimeUseConfigurer
    {
        private Mock<INuGetCachePrimer> _nugetCachePrimerMock;
        private Mock<INuGetCacheSentinel> _nugetCacheSentinelMock;
        private Mock<IEnvironmentProvider> _environmentProviderMock;

        public GivenADotnetFirstTimeUseConfigurer()
        {
            _nugetCachePrimerMock = new Mock<INuGetCachePrimer>();
            _nugetCacheSentinelMock = new Mock<INuGetCacheSentinel>();
            _environmentProviderMock = new Mock<IEnvironmentProvider>();

            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(false);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _environmentProviderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists_but_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environemnt_variable()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _environmentProviderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_primes_the_cache_if_the_sentinel_does_not_exist()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _environmentProviderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Once);
        }
    }
}
