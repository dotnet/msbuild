// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using Xunit;

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

        [Fact]
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
                    fileSystemMock.File,
                    fileSystemMock.Directory);

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void ItReturnsFalseIfTheSentinelDoesNotExist()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();

            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock.File,
                    fileSystemMock.Directory);

            firstTimeUseNoticeSentinel.Exists().Should().BeFalse();
        }

        [Fact]
        public void ItCreatesTheSentinelInTheDotnetUserProfileFolderPathIfItDoesNotExistAlready()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock.File,
                    fileSystemMock.Directory);

            firstTimeUseNoticeSentinel.Exists().Should().BeFalse();

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void ItDoesNotCreateTheSentinelAgainIfItAlreadyExistsInTheDotnetUserProfileFolderPath()
        {
            const string contentToValidateSentinalWasNotReplaced = "some string";
            var sentinel = Path.Combine(DOTNET_USER_PROFILE_FOLDER_PATH, FirstTimeUseNoticeSentinel.SENTINEL);
            _fileSystemMockBuilder.AddFile(sentinel, contentToValidateSentinalWasNotReplaced);

            var fileSystemMock = _fileSystemMockBuilder.Build();

            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock.File,
                    fileSystemMock.Directory);

            firstTimeUseNoticeSentinel.Exists().Should().BeTrue();

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            fileSystemMock.File.ReadAllText(sentinel).Should().Be(contentToValidateSentinalWasNotReplaced);
        }

        [Fact]
        public void ItCreatesTheDotnetUserProfileFolderIfItDoesNotExistAlreadyWhenCreatingTheSentinel()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var directoryMock = new DirectoryMock();
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock.File,
                    directoryMock);

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            directoryMock.Exists(DOTNET_USER_PROFILE_FOLDER_PATH).Should().BeTrue();
            directoryMock.CreateDirectoryInvoked.Should().BeTrue();
        }

        [Fact]
        public void ItDoesNotAttemptToCreateTheDotnetUserProfileFolderIfItAlreadyExistsWhenCreatingTheSentinel()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var directoryMock = new DirectoryMock(new List<string> { DOTNET_USER_PROFILE_FOLDER_PATH });
            var firstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    fileSystemMock.File,
                    directoryMock);

            firstTimeUseNoticeSentinel.CreateIfNotExists();

            directoryMock.CreateDirectoryInvoked.Should().BeFalse();
        }

        private class DirectoryMock : IDirectory
        {
            private IList<string> _directories;

            public bool CreateDirectoryInvoked { get; set; }

            public DirectoryMock(IList<string> directories = null)
            {
                _directories = directories ?? new List<string>();
            }

            public bool Exists(string path)
            {
                return _directories.Any(d => d == path);
            }

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> GetFiles(string path, string searchPattern)
            {
                throw new NotImplementedException();
            }

            public string GetDirectoryFullName(string path)
            {
                throw new NotImplementedException();
            }

            public void CreateDirectory(string path)
            {
                _directories.Add(path);
                CreateDirectoryInvoked = true;
            }

            public void Delete(string path, bool recursive)
            {
                throw new NotImplementedException();
            }
        }
    }
}
