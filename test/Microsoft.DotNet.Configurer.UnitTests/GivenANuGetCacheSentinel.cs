// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenANuGetCacheSentinel
    {
        private FileSystemMockBuilder _fileSystemMockBuilder;

        public GivenANuGetCacheSentinel()
        {
            _fileSystemMockBuilder = FileSystemMockBuilder.Create();
        }

        private const string NUGET_CACHE_PATH = "some path";

        [Fact]
        public void The_sentinel_has_the_current_version_in_its_name()
        {
            NuGetCacheSentinel.SENTINEL.Should().Contain($"{Product.Version}");
        }

        [Fact]
        public void It_returns_true_if_the_sentinel_exists()
        {
            _fileSystemMockBuilder.AddFiles(NUGET_CACHE_PATH, NuGetCacheSentinel.SENTINEL);

            var fileSystemMock = _fileSystemMockBuilder.Build();

            var nugetCacheSentinel = new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void It_returns_false_if_the_sentinel_does_not_exist()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();

            var nugetCacheSentinel = new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeFalse();
        }

        [Fact]
        public void It_creates_the_sentinel_in_the_nuget_cache_path_if_it_does_not_exist_already()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var nugetCacheSentinel = new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeFalse();

            nugetCacheSentinel.CreateIfNotExists();

            nugetCacheSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void It_does_not_create_the_sentinel_again_if_it_already_exists_in_the_nuget_cache_path()
        {
            const string contentToValidateSentinalWasNotReplaced = "some string";
            var sentinel = Path.Combine(NUGET_CACHE_PATH, NuGetCacheSentinel.SENTINEL);
            _fileSystemMockBuilder.AddFile(sentinel, contentToValidateSentinalWasNotReplaced);

            var fileSystemMock = _fileSystemMockBuilder.Build();

            var nugetCacheSentinel = new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File);

            nugetCacheSentinel.Exists().Should().BeTrue();

            nugetCacheSentinel.CreateIfNotExists();

            fileSystemMock.File.ReadAllText(sentinel).Should().Be(contentToValidateSentinalWasNotReplaced);
        }
    }
}
