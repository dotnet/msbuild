// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenANuGetCacheSentinel
    {
        private const string NUGET_CACHE_PATH = "some path";

        private FileSystemMockBuilder _fileSystemMockBuilder;

        public GivenANuGetCacheSentinel()
        {
            _fileSystemMockBuilder = FileSystemMockBuilder.Create();
        }        

        [Fact]
        public void As_soon_as_it_gets_created_it_tries_to_get_handle_of_the_InProgress_sentinel()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var fileMock = new FileMock();
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, fileSystemMock.Directory);

            fileMock.OpenFileWithRightParamsCalled.Should().BeTrue();
        }

        [Fact]
        public void It_sets_UnauthorizedAccess_to_false_when_no_UnauthorizedAccessException_happens()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var fileMock = new FileMock();
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, fileSystemMock.Directory);

            nugetCacheSentinel.UnauthorizedAccess.Should().BeFalse();
        }

        [Fact]
        public void It_sets_UnauthorizedAccess_to_true_when_an_UnauthorizedAccessException_happens()
        {
            var fileMock = new FileMock();
            var directoryMock = new DirectoryMock();
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, directoryMock);

            nugetCacheSentinel.UnauthorizedAccess.Should().BeTrue();
        }

        [Fact]
        public void It_returns_true_to_the_in_progress_sentinel_already_exists_when_it_fails_to_get_a_handle_to_it()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var fileMock = new FileMock();
            fileMock.InProgressSentinel = null;
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, fileSystemMock.Directory);

            nugetCacheSentinel.InProgressSentinelAlreadyExists().Should().BeTrue();
        }

        [Fact]
        public void It_returns_false_to_the_in_progress_sentinel_already_exists_when_it_fails_to_get_a_handle_to_it_but_it_failed_because_it_was_unauthorized()
        {
            var fileMock = new FileMock();
            var directoryMock = new DirectoryMock();
            fileMock.InProgressSentinel = null;
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, directoryMock);

            nugetCacheSentinel.InProgressSentinelAlreadyExists().Should().BeFalse();
        }

        [Fact]
        public void It_returns_false_to_the_in_progress_sentinel_already_exists_when_it_succeeds_in_getting_a_handle_to_it()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var fileMock = new FileMock();
            fileMock.InProgressSentinel = new MemoryStream();
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, fileSystemMock.Directory);

            nugetCacheSentinel.InProgressSentinelAlreadyExists().Should().BeFalse();
        }

        [Fact]
        public void It_disposes_of_the_handle_to_the_InProgressSentinel_when_NuGetCacheSentinel_is_disposed()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var mockStream = new MockStream();
            var fileMock = new FileMock();
            fileMock.InProgressSentinel = mockStream;
            using (var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileMock, fileSystemMock.Directory))
            {}

            mockStream.IsDisposed.Should().BeTrue();
        }

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

            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File, fileSystemMock.Directory);

            nugetCacheSentinel.Exists().Should().BeTrue();
        }

        [Fact]
        public void It_returns_false_if_the_sentinel_does_not_exist()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();

            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File, fileSystemMock.Directory);

            nugetCacheSentinel.Exists().Should().BeFalse();
        }

        [Fact]
        public void It_creates_the_sentinel_in_the_nuget_cache_path_if_it_does_not_exist_already()
        {
            var fileSystemMock = _fileSystemMockBuilder.Build();
            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File, fileSystemMock.Directory);

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

            var nugetCacheSentinel =
                new NuGetCacheSentinel(NUGET_CACHE_PATH, fileSystemMock.File, fileSystemMock.Directory);

            nugetCacheSentinel.Exists().Should().BeTrue();

            nugetCacheSentinel.CreateIfNotExists();

            fileSystemMock.File.ReadAllText(sentinel).Should().Be(contentToValidateSentinalWasNotReplaced);
        }

        private class DirectoryMock : IDirectory
        {
            public bool Exists(string path)
            {
                return false;
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
                throw new UnauthorizedAccessException();
            }
        }

        private class FileMock : IFile
        {
            public bool OpenFileWithRightParamsCalled { get; private set; }

            public Stream InProgressSentinel { get; set;}

            public bool Exists(string path)
            {
                throw new NotImplementedException();
            }

            public string ReadAllText(string path)
            {
                throw new NotImplementedException();
            }

            public Stream OpenRead(string path)
            {
                throw new NotImplementedException();
            }

            public Stream OpenFile(
                string path,
                FileMode fileMode,
                FileAccess fileAccess,
                FileShare fileShare,
                int bufferSize,
                FileOptions fileOptions)
            {
                Stream fileStream = null;

                var inProgressSentinel =
                    Path.Combine(GivenANuGetCacheSentinel.NUGET_CACHE_PATH, NuGetCacheSentinel.INPROGRESS_SENTINEL);

                if (path.Equals(inProgressSentinel) &&
                    fileMode == FileMode.OpenOrCreate &&
                    fileAccess == FileAccess.ReadWrite &&
                    fileShare == FileShare.None &&
                    bufferSize == 1 &&
                    fileOptions == FileOptions.DeleteOnClose)
                {
                    OpenFileWithRightParamsCalled = true;
                    fileStream = InProgressSentinel;
                }
                
                return fileStream;
            }

            public void CreateEmptyFile(string path)
            {
                throw new NotImplementedException();
            }

            public void WriteAllText(string path, string content)
            {
                throw new NotImplementedException();
            }
        }

        private class MockStream : MemoryStream
        {
            public bool IsDisposed { get; private set;}

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                IsDisposed = true;
            }
        }
    }
}
