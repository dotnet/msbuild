// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LockFilePatchingTests : TestBase
    {

        private static string ExportFilesRoot=> Path.Combine(RepoRoot, "TestAssets", "LockFiles", "ExportFiles");

        [Fact]
        public void TestExportFileIsParsed()
        {
            var lockFilePath = GetLockFilePath("valid");
            var lockFile = LockFileReader.Read(lockFilePath, designTime: false);

            var exportFile = lockFile.ExportFile;

            exportFile.Should().NotBeNull();
            exportFile.Exports.Count.Should().Be(3);
            exportFile.Exports.Should().OnlyHaveUniqueItems();

            // check export structure
            foreach (var export in exportFile.Exports)
            {
                export.TargetFramework.Should().NotBeNull();
                AssertTargetLibrary(export);
            }
        }

        [Fact]
        public void TestLockFileIsPatchedWithExportData()
        {
            var lockFilePath = GetLockFilePath("valid");
            var lockFile = LockFileReader.Read(lockFilePath, designTime: false);

            // check lock file structure is similar to export structure
            foreach (var target in lockFile.Targets)
            {
                target.Libraries.Count.Should().Be(3);

                foreach (var library in target.Libraries)
                {
                    AssertTargetLibrary(library);
                }
            }
        }

        [Fact]
        public void TestFragmentExistsButNoHolesInLockFile()
        {
            var lockFilePath = GetLockFilePath("valid_staleFragment");
            var lockFile = LockFileReader.Read(lockFilePath, designTime: false);

            var exportFile = lockFile.ExportFile;

            exportFile.Should().BeNull();

            lockFile.Targets.Count.Should().Be(1);

            lockFile.Targets[0].Libraries.Count.Should().Be(0);
        }

        [Fact]
        public void TestMissingExportFileThrows()
        {
            var lockFilePath = GetLockFilePath("invalid_nofragment");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath, designTime: false));
        }
        
        [Fact]
        public void TestMissingExportUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_nofragment");

            // not throw under design time scenario
            Assert.NotNull(LockFileReader.Read(lockFilePath, designTime: true));            
        }

        [Fact]
        public void TestMissingExportsThrow()
        {
            var lockFilePath = GetLockFilePath("invalid_missing-exports");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath, designTime: false));
        }

        [Fact]
        public void TestMissingExportsUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_missing-exports");

            // not throw under design time scenario
            Assert.NotNull(LockFileReader.Read(lockFilePath, designTime: true));
        }

        [Fact]
        public void TestMissmatchingFileVersionsThrows()
        {
            var lockFilePath = GetLockFilePath("invalid_missmatching-versions");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath, designTime: false));
        }

        [Fact]
        public void TestMissmatchingFileVersionsUnderDesignTime()
        {
            var lockFilePath = GetLockFilePath("invalid_missmatching-versions");

            Assert.NotNull(LockFileReader.Read(lockFilePath, designTime: true));
        }

        private static int LibraryNumberFromName(Microsoft.DotNet.ProjectModel.Graph.LockFileTargetLibrary library)
        {
            var libraryName = library.Name;
            return (int)char.GetNumericValue(libraryName[libraryName.Length - 1]);
        }

        private static void AssertTargetLibrary(Microsoft.DotNet.ProjectModel.Graph.LockFileTargetLibrary library)
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
