// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LockFilePatchingTests : TestBase
    {

        private static string ExportFilesRoot=> Path.Combine(RepoRoot, "TestAssets", "LockFiles", "ExportFiles");
    
        [Fact]
        public void TestMissingExportUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_nofragment");

            // not throw under design time scenario
            Assert.NotNull(new LockFileFormat().Read(lockFilePath));            
        }

        [Fact]
        public void TestMissingExportsUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_missing-exports");

            // not throw under design time scenario
            Assert.NotNull(new LockFileFormat().Read(lockFilePath));
        }
        
        [Fact]
        public void TestMissmatchingFileVersionsUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_missmatching-versions");

            Assert.NotNull(new LockFileFormat().Read(lockFilePath));
        }

        [Fact]
        public void TestPackageFoldersLoadCorrectly()
        {
            var lockFilePath = GetLockFilePath("valid");
            var lockFile = new LockFileFormat().Read(lockFilePath);

            Assert.Equal(2, lockFile.PackageFolders.Count);
            Assert.Equal("/foo/packages", lockFile.PackageFolders[0].Path);
            Assert.Equal("/foo/packages2", lockFile.PackageFolders[1].Path);
        }

        private static int LibraryNumberFromName(LockFileTargetLibrary library)
        {
            var libraryName = library.Name;
            return (int)char.GetNumericValue(libraryName[libraryName.Length - 1]);
        }

        private static void AssertTargetLibrary(LockFileTargetLibrary library)
        {
            var libraryNumber = LibraryNumberFromName(library);

            library.Type.Should().Be("project");

            library.Name.Should().Be("ClassLibrary" + libraryNumber);
            library.Version.ToNormalizedString().Should().Be("1.0.0");

            var dll = $"bin/Debug/ClassLibrary{libraryNumber}.dll";
            dll = dll.Replace('/', Path.DirectorySeparatorChar);

            library.CompileTimeAssemblies.Count.Should().Be(1);
            library.CompileTimeAssemblies.ElementAt(0).Path.Should().Be(dll);

            library.RuntimeAssemblies.Count.Should().Be(1);
            library.RuntimeAssemblies.ElementAt(0).Path.Should().Be(dll);
        }

        private static string GetLockFilePath(string exportSample)
        {
            return Path.Combine(ExportFilesRoot, exportSample, "project.lock.json");
        }
    }
}
