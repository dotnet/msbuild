// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
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
        private ITemporaryDirectoryMock _temporaryDirectoryMock;

        private Mock<ICommandFactory> _commandFactoryMock;
        private Mock<ICommand> _dotnetNewCommandMock;
        private Mock<ICommand> _dotnetRestoreCommandMock;
        private Mock<INuGetPackagesArchiver> _nugetPackagesArchiverMock;
        private Mock<INuGetCacheSentinel> _nugetCacheSentinel;

        public GivenANuGetCachePrimer()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            fileSystemMockBuilder.TemporaryFolder = TEMPORARY_FOLDER_PATH;
            fileSystemMockBuilder.AddFile(COMPRESSED_ARCHIVE_PATH);
            _fileSystemMock = fileSystemMockBuilder.Build();
            _temporaryDirectoryMock = (ITemporaryDirectoryMock)_fileSystemMock.Directory.CreateTemporaryDirectory();

            _commandFactoryMock = SetupCommandFactoryMock();

            _nugetPackagesArchiverMock = new Mock<INuGetPackagesArchiver>();
            _nugetPackagesArchiverMock.Setup(n => n.ExtractArchive()).Returns(PACKAGES_ARCHIVE_PATH);
            _nugetPackagesArchiverMock.Setup(n => n.NuGetPackagesArchive).Returns(COMPRESSED_ARCHIVE_PATH);

            _nugetCacheSentinel = new Mock<INuGetCacheSentinel>();

            var nugetCachePrimer = new NuGetCachePrimer(
                _commandFactoryMock.Object,
                _nugetPackagesArchiverMock.Object,
                _nugetCacheSentinel.Object,
                _fileSystemMock.Directory,
                _fileSystemMock.File);

            nugetCachePrimer.PrimeCache();
        }

        private Mock<ICommandFactory> SetupCommandFactoryMock()
        {
            var commandFactoryMock = new Mock<ICommandFactory>();

            _dotnetNewCommandMock = new Mock<ICommand>();
            SetupCommandMock(_dotnetNewCommandMock);
            commandFactoryMock
                .Setup(c => c.Create("new", Enumerable.Empty<string>(), null, Constants.DefaultConfiguration))
                .Returns(_dotnetNewCommandMock.Object);

            _dotnetRestoreCommandMock = new Mock<ICommand>();
            SetupCommandMock(_dotnetRestoreCommandMock);
            commandFactoryMock
                .Setup(c => c.Create(
                    "restore",
                    It.IsAny<IEnumerable<string>>(),
                    null,
                    Constants.DefaultConfiguration))
                .Returns(_dotnetRestoreCommandMock.Object);

            return commandFactoryMock;
        }

        private void SetupCommandMock(Mock<ICommand> commandMock)
        {
            commandMock
                .Setup(c => c.WorkingDirectory(TEMPORARY_FOLDER_PATH))
                .Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
        }

        [Fact]
        public void It_does_not_prime_the_NuGet_cache_if_the_archive_is_not_found_so_that_we_do_not_need_to_generate_the_archive_for_stage1()
        {
            var fileSystemMockBuilder = FileSystemMockBuilder.Create();
            var fileSystemMock = fileSystemMockBuilder.Build();

            var commandFactoryMock = SetupCommandFactoryMock();

            var nugetPackagesArchiverMock = new Mock<INuGetPackagesArchiver>();            
            nugetPackagesArchiverMock.Setup(n => n.NuGetPackagesArchive).Returns(COMPRESSED_ARCHIVE_PATH);

            var nugetCachePrimer = new NuGetCachePrimer(
                commandFactoryMock.Object,
                nugetPackagesArchiverMock.Object,
                _nugetCacheSentinel.Object,
                fileSystemMock.Directory,
                fileSystemMock.File);

            nugetCachePrimer.PrimeCache();

            nugetPackagesArchiverMock.Verify(n => n.ExtractArchive(), Times.Never);
            commandFactoryMock.Verify(c => c.Create(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                null,
                Constants.DefaultConfiguration), Times.Never);
        }

        [Fact]
        public void It_disposes_the_temporary_directory_created_for_the_temporary_project_used_to_prime_the_cache()
        {
            _temporaryDirectoryMock.DisposedTemporaryDirectory.Should().BeTrue();
        }

        [Fact]
        public void It_runs_dotnet_new_using_the_temporary_folder()
        {
            _dotnetNewCommandMock.Verify(c => c.WorkingDirectory(TEMPORARY_FOLDER_PATH), Times.Once);
        }

        [Fact]
        public void It_runs_dotnet_new_capturing_stdout()
        {
            _dotnetNewCommandMock.Verify(c => c.CaptureStdOut(), Times.Once);
        }

        [Fact]
        public void It_runs_dotnet_new_capturing_stderr()
        {
            _dotnetNewCommandMock.Verify(c => c.CaptureStdErr(), Times.Once);
        }

        [Fact]
        public void It_actually_runs_dotnet_new()
        {
            _dotnetNewCommandMock.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void It_uses_the_packages_archive_with_dotnet_restore()
        {
            _commandFactoryMock.Verify(
                c => c.Create(
                    "restore",
                    new [] {"-s", $"{PACKAGES_ARCHIVE_PATH}"},
                    null,
                    Constants.DefaultConfiguration),
                Times.Once);
        }

        [Fact]
        public void It_does_not_run_restore_if_dotnet_new_fails()
        {
            var commandFactoryMock = SetupCommandFactoryMock();
            _dotnetNewCommandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, -1, null, null));

            var nugetCachePrimer = new NuGetCachePrimer(
                commandFactoryMock.Object,
                _nugetPackagesArchiverMock.Object,
                _nugetCacheSentinel.Object,
                _fileSystemMock.Directory,
                _fileSystemMock.File);

            nugetCachePrimer.PrimeCache();

            commandFactoryMock.Verify(
                c => c.Create(
                    "restore",
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<NuGetFramework>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void It_runs_dotnet_restore_using_the_temporary_folder()
        {
            _dotnetRestoreCommandMock.Verify(c => c.WorkingDirectory(TEMPORARY_FOLDER_PATH), Times.Once);
        }

        [Fact]
        public void It_runs_dotnet_restore_capturing_stdout()
        {
            _dotnetRestoreCommandMock.Verify(c => c.CaptureStdOut(), Times.Once);
        }

        [Fact]
        public void It_runs_dotnet_restore_capturing_stderr()
        {
            _dotnetRestoreCommandMock.Verify(c => c.CaptureStdErr(), Times.Once);
        }

        [Fact]
        public void It_actually_runs_dotnet_restore()
        {
            _dotnetRestoreCommandMock.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void It_creates_a_sentinel_when_restore_succeeds()
        {
            _nugetCacheSentinel.Verify(n => n.CreateIfNotExists(), Times.Once);
        }

        [Fact]
        public void It_does_not_create_a_sentinel_when_restore_fails()
        {
            var nugetCacheSentinel = new Mock<INuGetCacheSentinel>();
            _dotnetRestoreCommandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, -1, null, null));

            var nugetCachePrimer = new NuGetCachePrimer(
                _commandFactoryMock.Object,
                _nugetPackagesArchiverMock.Object,
                nugetCacheSentinel.Object,
                _fileSystemMock.Directory,
                _fileSystemMock.File);

            nugetCachePrimer.PrimeCache();

            nugetCacheSentinel.Verify(n => n.CreateIfNotExists(), Times.Never);
        }
    }
}
