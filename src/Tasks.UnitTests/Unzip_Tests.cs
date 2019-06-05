// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Unzip_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Fact]
        public void CanOverwriteReadOnlyFile()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder source = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);
                TransientTestFile file1 = testEnvironment.CreateFile(source, "638AF4AE88A146E09CB69FE1CA7083DC.txt", "file1");

                new FileInfo(file1.Path).IsReadOnly = true;

                TransientZipArchive zipArchive = TransientZipArchive.Create(source, destination);

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(source.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) }
                };

                unzip.Execute().ShouldBeTrue(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain("638AF4AE88A146E09CB69FE1CA7083DC", () => _mockEngine.Log);
            }
        }

        [Fact]
        public void CanUnzip()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder source = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);
                testEnvironment.CreateFile(source, "BE78A17D30144B549D21F71D5C633F7D.txt", "file1");
                testEnvironment.CreateFile(source, "A04FF4B88DF14860B7C73A8E75A4FB76.txt", "file2");

                TransientZipArchive zipArchive = TransientZipArchive.Create(source, testEnvironment.CreateFolder(createFolder: true));

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) }
                };

                unzip.Execute().ShouldBeTrue(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "BE78A17D30144B549D21F71D5C633F7D.txt"), () => _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "A04FF4B88DF14860B7C73A8E75A4FB76.txt"), () => _mockEngine.Log);
            }
        }

        [PlatformSpecific(TestPlatforms.Windows)] // Can't figure out how to make CreateDirectory throw on non-Windows
        [Fact]
        public void LogsErrorIfDirectoryCannotBeCreated()
        {
            Unzip unzip = new Unzip
            {
                BuildEngine = _mockEngine,
                DestinationFolder = new TaskItem(String.Empty)
            };

            unzip.Execute().ShouldBeFalse(() => _mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB3931", () => _mockEngine.Log);
        }

        public static bool NotRunningAsRoot()
        {
            if (NativeMethodsShared.IsWindows)
            {
                return true;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            var process = Process.Start(psi);

            process.WaitForExit((int)TimeSpan.FromSeconds(2).TotalMilliseconds).ShouldBeTrue();

            return process.StandardOutput.ReadToEnd().Trim() != "0";
        }

        [ConditionalFact(nameof(NotRunningAsRoot))] // root can write to read-only files
        public void LogsErrorIfReadOnlyFileCannotBeOverwitten()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder source = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);
                TransientTestFile file1 = testEnvironment.CreateFile(source, "D6DFD219DACE48F8B86EFCDF98433333.txt", "file1");

                new FileInfo(file1.Path).IsReadOnly = true;

                TransientZipArchive zipArchive = TransientZipArchive.Create(source, destination);

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(source.Path),
                    OverwriteReadOnlyFiles = false,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) }
                };

                unzip.Execute().ShouldBeFalse(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain($"D6DFD219DACE48F8B86EFCDF98433333.txt{ (NativeMethodsShared.IsMono ? "\"" : "'") } is denied", () => _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfSourceFileCannotBeOpened()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: false);

                TransientTestFile file = testEnvironment.CreateFile("foo.txt", "foo");

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(folder.Path),
                    SourceFiles = new ITaskItem[] { new TaskItem(file.Path), }
                };

                unzip.Execute().ShouldBeFalse(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3933", () => _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfSourceFileDoesNotExist()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: false);

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(folder.Path),
                    SourceFiles = new ITaskItem[] { new TaskItem(Path.Combine(testEnvironment.DefaultTestDirectory.Path, "foo.zip")), }
                };

                unzip.Execute().ShouldBeFalse(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3932", () => _mockEngine.Log);
            }
        }
    }
}
