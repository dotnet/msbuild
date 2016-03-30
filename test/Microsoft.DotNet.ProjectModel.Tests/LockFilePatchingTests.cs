// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LockFilePatchingTests : TestBase
    {

        private string ExportFilesRoot=> Path.Combine(RepoRoot, "TestAssets", "LockFiles", "ExportFiles");

        [Fact]
        public void TestValidPatching()
        {
            var lockFilePath = GetLockFilePath("valid");
            var lockFile = LockFileReader.Read(lockFilePath);

            var exportFile = lockFile.ExportFile;

            exportFile.Should().NotBeNull();
            exportFile.Exports.Count.Should().Be(3);
            exportFile.Exports.Should().OnlyHaveUniqueItems();

            // check export structure
            for (int i = 0; i < 3; i++)
            {
                var export = exportFile.Exports.ToList().ElementAt(i);

                export.TargetFramework.Should().NotBeNull();

                AssertTargetLibrary(i + 1, export);
            }

            lockFile.Targets.Count.Should().Be(3);

            // check lock file structure is similar to export structure
            foreach (var target in lockFile.Targets)
            {
                target.Libraries.Count.Should().Be(3);

                for (int i = 0; i < 3; i++)
                {
                    var targetLibrary = target.Libraries.ElementAt(i);
                    AssertTargetLibrary(i + 1, targetLibrary);
                }
            }
        }

        [Fact]
        public void TestFragmentExistsButNoHolesInLockFile()
        {
            var lockFilePath = GetLockFilePath("valid_staleFragment");
            var lockFile = LockFileReader.Read(lockFilePath);

            var exportFile = lockFile.ExportFile;

            exportFile.Should().BeNull();

            lockFile.Targets.Count.Should().Be(1);

            lockFile.Targets[0].Libraries.Count.Should().Be(0);
        }

        [Fact]
        public void TestMissingExportFileThrows()
        {
            var lockFilePath = GetLockFilePath("invalid_nofragment");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath));
        }

        [Fact]
        public void TestMissingExportsThrow()
        {
            var lockFilePath = GetLockFilePath("invalid_missing-exports");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath));
        }

        [Fact]
        public void TestMissmatchingFileVersionsThrows()
        {
            var lockFilePath = GetLockFilePath("invalid_missmatching-versions");

            Assert.Throws<FileFormatException>(() => LockFileReader.Read(lockFilePath));
        }

        private static void AssertTargetLibrary(int i, LockFileTargetLibrary export)
        {
            export.Type.Should().Be("project");

            export.Name.Should().Be("ClassLibrary" + i);
            export.Version.ToNormalizedString().Should().Be("1.0.0");

            var dll = $"bin/Debug/ClassLibrary{i}.dll";
            dll = dll.Replace('/', Path.DirectorySeparatorChar);

            export.CompileTimeAssemblies.Count.Should().Be(1);
            export.CompileTimeAssemblies.ElementAt(0).Path.Should().Be(dll);

            export.RuntimeAssemblies.Count.Should().Be(1);
            export.RuntimeAssemblies.ElementAt(0).Path.Should().Be(dll);
        }

        private string GetLockFilePath(string exportSample)
        {
            return Path.Combine(ExportFilesRoot, exportSample, "project.lock.json");
        }
    }
}
