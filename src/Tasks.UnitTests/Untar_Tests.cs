// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Untar relies on System.Formats.Tar which is only available on .NET (not .NET Framework).
#if NET

using System;
using System.IO;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Untar_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Theory]
        [InlineData(null)]
        [InlineData("None")]
        [InlineData("GZip")]
        [InlineData("ZStandard")]
        public void CanUntar(string? compression)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "F1.txt", "F1");
                testEnvironment.CreateFile(sourceFolder, "F2.txt", "F2");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeTrue(_mockEngine.Log);

                File.ReadAllText(Path.Combine(destination.Path, "F1.txt")).ShouldBe("F1");
                File.ReadAllText(Path.Combine(destination.Path, "F2.txt")).ShouldBe("F2");
            }
        }

        [Fact]
        public void CanUntarWithIncludeFilter()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "included.txt", "included");
                testEnvironment.CreateFile(sourceFolder, "excluded.txt", "excluded");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    Include = "included.txt",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeTrue(_mockEngine.Log);

                File.Exists(Path.Combine(destination.Path, "included.txt")).ShouldBeTrue(_mockEngine.Log);
                File.Exists(Path.Combine(destination.Path, "excluded.txt")).ShouldBeFalse(_mockEngine.Log);
            }
        }

        [Fact]
        public void CanUntarWithExcludeFilter()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "kept.txt", "kept");
                testEnvironment.CreateFile(sourceFolder, "dropped.txt", "dropped");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    Exclude = "dropped.txt",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeTrue(_mockEngine.Log);

                File.Exists(Path.Combine(destination.Path, "kept.txt")).ShouldBeTrue(_mockEngine.Log);
                File.Exists(Path.Combine(destination.Path, "dropped.txt")).ShouldBeFalse(_mockEngine.Log);
            }
        }

        [Fact]
        public void SkipsUnchangedFiles()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "unchanged.txt", "unchanged");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar CreateTask() => new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SkipUnchangedFiles = true,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                CreateTask().Execute().ShouldBeTrue(_mockEngine.Log);

                // A second extraction should skip the unchanged file.
                CreateTask().Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(nameof(Untar.SkipUnchangedFiles), customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void CanOverwriteExistingFile()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "file.txt", "new-content");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(destination, "file.txt", "old-content");

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeTrue(_mockEngine.Log);

                File.ReadAllText(Path.Combine(destination.Path, "file.txt")).ShouldBe("new-content");
            }
        }

        [Fact]
        public void LogsErrorIfSourceFileDoesNotExist()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SourceFiles = [new TaskItem(Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "missing.tar"))],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4332", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorForCorruptArchive()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFile corrupt = testEnvironment.CreateFile(folder, "corrupt.tar", "this is not a tar archive");

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SourceFiles = [new TaskItem(corrupt.Path)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4333", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfIncludeContainsPropertyReferences()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "file.txt", "file");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    SourceFiles = [new TaskItem(tarFilePath)],
                    Include = "$(Include)",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4338", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void FailIfNotIncrementalLogsError()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(sourceFolder, "file.txt", "file");

                string tarFilePath = CreateTar(testEnvironment, sourceFolder, compression: null);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    FailIfNotIncremental = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = [new TaskItem(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);
            }
        }

        private string CreateTar(TestEnvironment testEnvironment, TransientTestFolder sourceFolder, string? compression)
        {
            string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

            TarDirectory tarDirectory = new TarDirectory
            {
                BuildEngine = _mockEngine,
                Compression = compression,
                DestinationFile = new TaskItem(tarFilePath),
                SourceDirectory = new TaskItem(sourceFolder.Path),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

            return tarFilePath;
        }
    }
}

#endif
