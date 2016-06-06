// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenANuGetCacheSentinel
    {
        private const string NUGET_CACHE_PATH = "some path";

        [Fact]
        public void The_sentinel_has_the_current_version_in_its_name()
        {
            NuGetCacheSentinel.SENTINEL.Should().Contain($"{Product.Version}");
        }

        [Fact]
        public void It_returns_true_if_the_sentinel_exists()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            fileSystemMockBuilder.AddFiles(NUGET_CACHE_PATH, NuGetCacheSentinel.SENTINEL);

            var fileSystemMock = fileSystemMockBuilder.Build();

            var nugetCacheSentinel = new NuGetCacheSentinel(fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void It_returns_false_if_the_sentinel_does_not_exist()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            var fileSystemMock = fileSystemMockBuilder.Build();

            var nugetCacheSentinel = new NuGetCacheSentinel(fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeTrue();
        }
    }
}
