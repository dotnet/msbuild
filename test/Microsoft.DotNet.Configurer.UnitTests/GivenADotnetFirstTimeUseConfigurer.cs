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

        public GivenADotnetFirstTimeUseConfigurer()
        {
            _nugetCachePrimerMock = new Mock<INuGetCachePrimer>();
            _nugetCacheSentinelMock = new Mock<INuGetCacheSentinel>();
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_primes_the_cache_if_the_sentinel_does_not_exist()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Once);
        }
    }
}
