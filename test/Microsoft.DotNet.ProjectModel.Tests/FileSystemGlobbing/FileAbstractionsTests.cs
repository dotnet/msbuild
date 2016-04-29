// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.TestUtility;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests
{
    public class FileAbstractionsTests
    {
        [Fact]
        public void TempFolderStartsInitiallyEmpty()
        {
            using (var scenario = new DisposableFileSystem())
            {
                var contents = scenario.DirectoryInfo.EnumerateFileSystemInfos();

                Assert.Equal(Path.GetFileName(scenario.RootPath), scenario.DirectoryInfo.Name);
                Assert.Equal(scenario.RootPath, scenario.DirectoryInfo.FullName);
                Assert.Equal(0, contents.Count());
            }
        }

        [Fact]
        public void FilesAreEnumerated()
        {
            using (var scenario = new DisposableFileSystem()
                .CreateFile("alpha.txt"))
            {
                var contents = new DirectoryInfoWrapper(scenario.DirectoryInfo).EnumerateFileSystemInfos();
                var alphaTxt = contents.OfType<FileInfoBase>().Single();

                Assert.Equal(1, contents.Count());
                Assert.Equal("alpha.txt", alphaTxt.Name);
            }
        }

        [Fact]
        public void FoldersAreEnumerated()
        {
            using (var scenario = new DisposableFileSystem()
                .CreateFolder("beta"))
            {
                var contents1 = new DirectoryInfoWrapper(scenario.DirectoryInfo).EnumerateFileSystemInfos();
                var beta = contents1.OfType<DirectoryInfoBase>().Single();
                var contents2 = beta.EnumerateFileSystemInfos();

                Assert.Equal(1, contents1.Count());
                Assert.Equal("beta", beta.Name);
                Assert.Equal(0, contents2.Count());
            }
        }

        [Fact]
        public void SubFoldersAreEnumerated()
        {
            using (var scenario = new DisposableFileSystem()
                .CreateFolder("beta")
                .CreateFile(Path.Combine("beta", "alpha.txt")))
            {
                var contents1 = new DirectoryInfoWrapper(scenario.DirectoryInfo).EnumerateFileSystemInfos();
                var beta = contents1.OfType<DirectoryInfoBase>().Single();
                var contents2 = beta.EnumerateFileSystemInfos();
                var alphaTxt = contents2.OfType<FileInfoBase>().Single();

                Assert.Equal(1, contents1.Count());
                Assert.Equal("beta", beta.Name);
                Assert.Equal(1, contents2.Count());
                Assert.Equal("alpha.txt", alphaTxt.Name);
            }
        }

        [Fact]
        public void GetDirectoryCanTakeDotDot()
        {
            using (var scenario = new DisposableFileSystem()
                .CreateFolder("gamma")
                .CreateFolder("beta")
                .CreateFile(Path.Combine("beta", "alpha.txt")))
            {
                var directoryInfoBase = new DirectoryInfoWrapper(scenario.DirectoryInfo);
                var gamma = directoryInfoBase.GetDirectory("gamma");
                var dotdot = gamma.GetDirectory("..");
                var contents1 = dotdot.EnumerateFileSystemInfos();
                var beta = dotdot.GetDirectory("beta");
                var contents2 = beta.EnumerateFileSystemInfos();
                var alphaTxt = contents2.OfType<FileInfoBase>().Single();

                Assert.Equal("..", dotdot.Name);
                Assert.Equal(2, contents1.Count());
                Assert.Equal("beta", beta.Name);
                Assert.Equal(1, contents2.Count());
                Assert.Equal("alpha.txt", alphaTxt.Name);
            }
        }
    }
}