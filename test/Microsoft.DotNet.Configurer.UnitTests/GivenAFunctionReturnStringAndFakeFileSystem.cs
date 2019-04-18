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
    public class GivenAFunctionReturnStringAndFakeFileSystem
    {
        private const string DOTNET_USER_PROFILE_FOLDER_PATH = "some path";

        private FileSystemMockBuilder _fileSystemMockBuilder;
        private UserLevelCacheWriter _userLevelCacheWriter;
        private IFileSystem _fileSystemMock;

        public GivenAFunctionReturnStringAndFakeFileSystem()
        {
            _fileSystemMockBuilder = FileSystemMockBuilder.Create();
            _fileSystemMock = _fileSystemMockBuilder.Build();
            _userLevelCacheWriter =
                new UserLevelCacheWriter(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    _fileSystemMock.File,
                    _fileSystemMock.Directory);
        }

        [Fact]
        public void ItReturnsTheFunctionResult()
        {
            _userLevelCacheWriter.RunWithCache("fooKey", () => "foo").Should().Be("foo");
        }

        [Fact]
        public void ItRunsTheFunctionOnlyOnceWhenInvokeTwice()
        {
            var counter = new Counter();
            Func<string> func = () =>
            {
                counter.Increase();
                return "foo";
            };

            _userLevelCacheWriter.RunWithCache("fookey", func).Should().Be("foo");
            _userLevelCacheWriter.RunWithCache("fookey", func).Should().Be("foo");
            counter.Count.Should().Be(1);
        }

        [Fact]
        public void ItKeepsTheCacheInUserProfileWithCacheKey()
        {
            _userLevelCacheWriter.RunWithCache("fooKey", () => "foo");
            var path = Path.Combine("some path", $"{Product.Version}_fooKey.dotnetUserLevelCache");
            _fileSystemMock.File.Exists(path);
            _fileSystemMock.File.ReadAllText(path).Should().Be("foo");
        }

        [Fact]
        public void ItRunsAndReturnsTheValueIfCacheCreationFailed()
        {
            var mockFile = new Mock<IFile>();

            var systemUndertest =
                new UserLevelCacheWriter(
                    DOTNET_USER_PROFILE_FOLDER_PATH,
                    new NoPermissionFileFake(),
                    new NoPermissionDirectoryFake());

            var counter = new Counter();
            Func<string> func = () =>
            {
                counter.Increase();
                return "foo";
            };

            systemUndertest.RunWithCache("fookey", func).Should().Be("foo");
            systemUndertest.RunWithCache("fookey", func).Should().Be("foo");
            counter.Count.Should().Be(2);
        }

        private class NoPermissionFileFake : IFile
        {
            public bool Exists(string path)
            {
                return false;
            }

            public string ReadAllText(string path)
            {
                throw new UnauthorizedAccessException();
            }

            public Stream OpenRead(string path)
            {
                throw new UnauthorizedAccessException();
            }

            public Stream OpenFile(
                string path,
                FileMode fileMode,
                FileAccess fileAccess,
                FileShare fileShare,
                int bufferSize,
                FileOptions fileOptions)
            {
                throw new NotImplementedException();
            }

            public void CreateEmptyFile(string path)
            {
                throw new UnauthorizedAccessException();
            }

            public void WriteAllText(string path, string content)
            {
                throw new UnauthorizedAccessException();
            }

            public void Move(string source, string destination)
            {
                throw new UnauthorizedAccessException();
            }

            public void Delete(string path)
            {
                throw new UnauthorizedAccessException();
            }

            public void Copy(string source, string destination)
            {
                throw new UnauthorizedAccessException();
            }
        }

        private class NoPermissionDirectoryFake : IDirectory
        {

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFiles(string path)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFileSystemEntries(string path)
            {
                throw new UnauthorizedAccessException();
            }

            public string GetCurrentDirectory()
            {
                throw new NotImplementedException();
            }

            public bool Exists(string path)
            {
                return false;
            }

            public void CreateDirectory(string path)
            {
                throw new UnauthorizedAccessException();
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

        private class Counter
        {
            public int Count { get; private set; }
            public void Increase() { Count++; }
        }
    }
}
