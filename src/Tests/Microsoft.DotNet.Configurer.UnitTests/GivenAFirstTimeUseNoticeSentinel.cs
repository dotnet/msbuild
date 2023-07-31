// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenAFirstTimeUseNoticeSentinel
    {
        private const string DOTNET_USER_PROFILE_FOLDER_PATH = "some path";

        private FileSystemMockBuilder _fileSystemMockBuilder;

        public GivenAFirstTimeUseNoticeSentinel()
        {
            _fileSystemMockBuilder = FileSystemMockBuilder.Create();
        }

        [Fact(Skip = "Product.Version not set correctly when running tests")]
        public void TheSentinelHasTheCurrentVersionInItsName()
        {
            FirstTimeUseNoticeSentinel.SENTINEL.Should().Contain($"{Product.Version}");
        }

        [Fact]
        public void ItReturnsTrueIfTheSentinelExists()
        {
            _fileSystemMockBuilder.AddFiles(DOTNET_USER_PROFILE_FOLDER_PATH, FirstTimeUseNoticeSentinel.SENTINEL);

            var fileSystemMock = _fileSystemMockBuilder.Build();

            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock);

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void ItReturnsFalseIfTheSentinelDoesNotExist()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();

            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock);

            firstTimeUseNoticeSentinel.Exists().Should().BeFalse();
        }

        [Fact]
        public void ItCreatesTheSentinelInTheDotnetUserProfileFolderPathIfItDoesNotExistAlready()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock);

            firstTimeUseNoticeSentinel.Exists().Should().BeFalse();

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void ItDoesNotCreateTheSentinelAgainIfItAlreadyExistsInTheDotnetUserProfileFolderPath()
        {
            const string contentToValidateSentinelWasNotReplaced = "some string";
            var sentinel = Path.Combine(DOTNET_USER_PROFILE_FOLDER_PATH, FirstTimeUseNoticeSentinel.SENTINEL);
            _fileSystemMockBuilder.AddFile(sentinel, contentToValidateSentinelWasNotReplaced);

            var fileSystemMock = _fileSystemMockBuilder.Build();

            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock);

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            fileSystemMock.File.ReadAllText(sentinel).Should().Be(contentToValidateSentinelWasNotReplaced);
        }

        [Fact]
        public void ItCreatesTheDotnetUserProfileFolderIfItDoesNotExistAlreadyWhenCreatingTheSentinel()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var directoryMock = new DirectoryMockWithSpy(fileSystemMock.Directory);
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    new FileSystemMock(
                        fileSystemMock.File,
                        directoryMock));

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            directoryMock.Exists(DOTNET_USER_PROFILE_FOLDER_PATH).Should().BeTrue();
            directoryMock.CreateDirectoryInvoked.Should().BeTrue();
        }

        [Fact]
        public void ItDoesNotAttemptToCreateTheDotnetUserProfileFolderIfItAlreadyExistsWhenCreatingTheSentinel()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var directoryMock = new DirectoryMockWithSpy(fileSystemMock.Directory, new List<string> { DOTNET_USER_PROFILE_FOLDER_PATH });
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    new FileSystemMock(
                        fileSystemMock.File,
                        directoryMock));

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            directoryMock.CreateDirectoryInvoked.Should().BeFalse();
        }

        private class FileSystemMock : IFileSystem
        {
            public FileSystemMock(IFile file, IDirectory directory)
            {
                File = file;
                Directory = directory;
            }

            public IFile File { get; private set; }

            public IDirectory Directory { get; private set; }
        }

        private class DirectoryMockWithSpy : IDirectory
        {
            private readonly IDirectory _directorySystem;

            public bool CreateDirectoryInvoked { get; set; }

            public DirectoryMockWithSpy(IDirectory directorySystem, IList<string> directories = null)
            {
                if (directorySystem != null) _directorySystem = directorySystem;

                if (directories != null)
                {
                    foreach (var directory in directories)
                    {
                        _directorySystem.CreateDirectory(directory);
                    }
                }
            }

            public bool Exists(string path)
            {
                return _directorySystem.Exists(path);
            }

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                throw new NotImplementedException();
            }

            public string CreateTemporarySubdirectory()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateDirectories(string path)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFiles(string path)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFileSystemEntries(string path)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
            {
                throw new NotImplementedException();
            }

            public string GetCurrentDirectory()
            {
                throw new NotImplementedException();
            }

            public void CreateDirectory(string path)
            {
                _directorySystem.CreateDirectory(path);
                CreateDirectoryInvoked = true;
            }

            public void Delete(string path, bool recursive)
            {
                throw new NotImplementedException();
            }

            public void Move(string source, string destination)
            {
                throw new NotImplementedException();
            }
        }
    }
}
