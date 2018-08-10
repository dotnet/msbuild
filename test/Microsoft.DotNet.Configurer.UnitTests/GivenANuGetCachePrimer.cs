// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities.Mock;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenANuGetCachePrimer
    {
        private const string COMPRESSED_ARCHIVE_PATH = "a path to somewhere";
        private const string TEMPORARY_FOLDER_PATH = "some path";
        private const string PACKAGES_ARCHIVE_PATH = "some other path";

        private IFileSystem _fileSystemMock;

        private Mock<INuGetPackagesArchiver> _nugetPackagesArchiverMock;
        private Mock<INuGetCacheSentinel> _nugetCacheSentinel;
        private CliFolderPathCalculator _cliFolderPathCalculator;

        public GivenANuGetCachePrimer()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            fileSystemMockBuilder.TemporaryFolder = TEMPORARY_FOLDER_PATH;
            fileSystemMockBuilder.AddFile(COMPRESSED_ARCHIVE_PATH);
            _fileSystemMock = fileSystemMockBuilder.Build();
            
            _nugetPackagesArchiverMock = new Mock<INuGetPackagesArchiver>();
            _nugetPackagesArchiverMock.Setup(n => n.NuGetPackagesArchive).Returns(COMPRESSED_ARCHIVE_PATH);

            _nugetCacheSentinel = new Mock<INuGetCacheSentinel>();

            _cliFolderPathCalculator = new CliFolderPathCalculator();

            var nugetCachePrimer = new NuGetCachePrimer(
                _nugetPackagesArchiverMock.Object,
                _nugetCacheSentinel.Object,
                _cliFolderPathCalculator,
                _fileSystemMock.File);

            nugetCachePrimer.PrimeCache();
        }

        [Fact]
        public void It_does_not_prime_the_NuGet_cache_if_the_archive_is_not_found_so_that_we_do_not_need_to_generate_the_archive_for_stage1()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            var fileSystemMock = fileSystemMockBuilder.Build();

            var nugetPackagesArchiverMock = new Mock<INuGetPackagesArchiver>();            
            nugetPackagesArchiverMock.Setup(n => n.NuGetPackagesArchive).Returns(COMPRESSED_ARCHIVE_PATH);

            var nugetCachePrimer = new NuGetCachePrimer(
                nugetPackagesArchiverMock.Object,
                _nugetCacheSentinel.Object,
                _cliFolderPathCalculator,
                fileSystemMock.File);

            nugetCachePrimer.PrimeCache();

            nugetPackagesArchiverMock.Verify(n => n.ExtractArchive(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_extracts_the_archive_to_the_fallback_folder()
        {
            _nugetPackagesArchiverMock.Verify(n =>
                n.ExtractArchive(_cliFolderPathCalculator.CliFallbackFolderPath),
                Times.Exactly(1));
        }

        [Fact]
        public void It_creates_a_sentinel_when_restore_succeeds()
        {
            _nugetCacheSentinel.Verify(n => n.CreateIfNotExists(), Times.Once);
        }

        [Fact]
        public void It_does_not_create_a_sentinel_when_extracting_the_archive_fails()
        {
            var nugetCacheSentinel = new Mock<INuGetCacheSentinel>();
            var nugetPackagesArchiveMock = new Mock<INuGetPackagesArchiver>();
            nugetPackagesArchiveMock.Setup(n => n.ExtractArchive(It.IsAny<string>())).Throws<Exception>();

            var nugetCachePrimer = new NuGetCachePrimer(
                nugetPackagesArchiveMock.Object,
                nugetCacheSentinel.Object,
                _cliFolderPathCalculator,
                _fileSystemMock.File);

            Action action = () => nugetCachePrimer.PrimeCache();

            action.ShouldThrow<Exception>();
            nugetCacheSentinel.Verify(n => n.CreateIfNotExists(), Times.Never);
        }
    }
}
