// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Untar relies on System.Formats.Tar which is only available on .NET (not .NET Framework).
#if NET

using System;
using System.IO;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Untar_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Theory]
        [InlineData(TarDirectory.TarCompression.None)]
        [InlineData(TarDirectory.TarCompression.GZip)]
        [InlineData(TarDirectory.TarCompression.ZStandard)]
        public void CanUntar(TarDirectory.TarCompression compression)
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
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar CreateTask() => new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = true,
                    SourceFiles = [new FileInfo(tarFilePath)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: true);
                testEnvironment.CreateFile(destination, "file.txt", "old-content");

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
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
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SourceFiles = [new FileInfo(Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "missing.tar"))],
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
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SourceFiles = [new FileInfo(corrupt.Path)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SourceFiles = [new FileInfo(tarFilePath)],
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

                string tarFilePath = CreateTar(testEnvironment, sourceFolder);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    FailIfNotIncremental = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);
            }
        }

        [Fact]
        public void RejectsTarSlipEntryOutsideDestination()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                // Craft a malicious archive whose entry name traverses out of the destination directory.
                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "malicious.tar");
                string maliciousEntryName = $"..{Path.DirectorySeparatorChar}escaped.txt";

                using (FileStream tarStream = new FileStream(tarFilePath, FileMode.Create, FileAccess.Write))
                using (System.Formats.Tar.TarWriter writer = new System.Formats.Tar.TarWriter(tarStream, System.Formats.Tar.TarEntryFormat.Pax))
                {
                    System.Formats.Tar.PaxTarEntry entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, maliciousEntryName)
                    {
                        DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("pwned")),
                    };
                    writer.WriteEntry(entry);
                }

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeFalse(_mockEngine.Log);

                // The traversal target must not have been written outside the destination directory.
                string escapedPath = Path.GetFullPath(Path.Combine(destination.Path, maliciousEntryName));
                File.Exists(escapedPath).ShouldBeFalse();

                // The failure must surface the dedicated "outside destination directory" error (MSB4334),
                // not a generic "could not open file" (MSB4333).
                _mockEngine.Log.ShouldContain("MSB4334");
            }
        }

        [UnixOnlyFact]
        public void MasksSpecialPermissionBitsByDefault()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                string tarFilePath = CreateTarWithMode(testEnvironment, "F1.txt", "F1", UnixFileMode.SetGroup | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);

                Untar untar = new Untar
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new DirectoryInfo(destination.Path),
                    SkipUnchangedFiles = false,
                    SourceFiles = [new FileInfo(tarFilePath)],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                untar.Execute().ShouldBeTrue(_mockEngine.Log);

#pragma warning disable CA1416 // Validate platform compatibility — guarded by [UnixOnlyFact]
                UnixFileMode extractedMode = File.GetUnixFileMode(Path.Combine(destination.Path, "F1.txt"));
#pragma warning restore CA1416

                // The setgid bit and any other special bits must be dropped when preserving is off.
                (extractedMode & UnixFileMode.SetGroup).ShouldBe(UnixFileMode.None);
                (extractedMode & UnixFileMode.SetUser).ShouldBe(UnixFileMode.None);
                (extractedMode & UnixFileMode.StickyBit).ShouldBe(UnixFileMode.None);
            }
        }

        private string CreateTarWithMode(TestEnvironment testEnvironment, string entryName, string content, UnixFileMode mode)
        {
            string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "modes.tar");

            using (FileStream tarStream = new FileStream(tarFilePath, FileMode.Create, FileAccess.Write))
            using (System.Formats.Tar.TarWriter writer = new System.Formats.Tar.TarWriter(tarStream))
            {
                System.Formats.Tar.UstarTarEntry entry = new System.Formats.Tar.UstarTarEntry(System.Formats.Tar.TarEntryType.RegularFile, entryName)
                {
                    Mode = mode,
                    DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
                };

                writer.WriteEntry(entry);
            }

            return tarFilePath;
        }

        private string CreateTar(TestEnvironment testEnvironment, TransientTestFolder sourceFolder, TarDirectory.TarCompression compression = TarDirectory.TarCompression.None)
        {
            string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

            TarDirectory tarDirectory = new TarDirectory
            {
                BuildEngine = _mockEngine,
                Compression = compression,
                DestinationFile = new FileInfo(tarFilePath),
                SourceDirectory = new DirectoryInfo(sourceFolder.Path),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

            return tarFilePath;
        }
    }
}

#endif


