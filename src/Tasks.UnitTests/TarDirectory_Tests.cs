// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TarDirectory relies on System.Formats.Tar which is only available on .NET (not .NET Framework).
#if NET

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class TarDirectory_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Theory]
        [InlineData(TarDirectory.TarCompression.None)]
        [InlineData(TarDirectory.TarCompression.GZip)]
        [InlineData(TarDirectory.TarCompression.ZStandard)]
        public void CanTarDirectory(TarDirectory.TarCompression compression)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "6DE6060259C44DB6B145159376751C22.txt", "6DE6060259C44DB6B145159376751C22");
                testEnvironment.CreateFile(sourceFolder, "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt", "CDA3DD8C25A54A7CAC638A444CB1EAD0");

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

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(tarFilePath, customMessage: _mockEngine.Log);

                // Should not contain any warnings in the TarDirectory bucket (MSB4321 - MSB4330).
                _mockEngine.Log.ShouldNotContain("MSB432", customMessage: _mockEngine.Log);

                GetTarEntryNames(tarFilePath, compression)
                    .ShouldBe(
                        [
                            "6DE6060259C44DB6B145159376751C22.txt",
                            "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt"
                        ],
                        ignoreOrder: true);
            }
        }

        [Fact]
        public void CanOverwriteExistingFile()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "F1C22D660B0D4DAAA296C1B980320B03.txt", "F1C22D660B0D4DAAA296C1B980320B03");
                testEnvironment.CreateFile(sourceFolder, "AA825D1CB154492BAA58E1002CE1DFEB.txt", "AA825D1CB154492BAA58E1002CE1DFEB");

                TransientTestFile file = testEnvironment.CreateFile(testEnvironment.DefaultTestDirectory, "test.tar", contents: "test");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new FileInfo(file.Path),
                    Overwrite = true,
                    SourceDirectory = new DirectoryInfo(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(file.Path, customMessage: _mockEngine.Log);

                GetTarEntryNames(file.Path, TarDirectory.TarCompression.None)
                    .ShouldBe(
                        [
                            "F1C22D660B0D4DAAA296C1B980320B03.txt",
                            "AA825D1CB154492BAA58E1002CE1DFEB.txt"
                        ],
                        ignoreOrder: true);
            }
        }

        [Fact]
        public void LogsErrorIfDestinationExists()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);

                TransientTestFile file = testEnvironment.CreateFile("foo.tar", "foo");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new FileInfo(file.Path),
                    SourceDirectory = new DirectoryInfo(folder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4322", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfDirectoryDoesNotExist()
        {
            TarDirectory tarDirectory = new TarDirectory
            {
                BuildEngine = _mockEngine,
                DestinationFile = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "test.tar")),
                SourceDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB4321", customMessage: _mockEngine.Log);
        }

        [Theory]
        [InlineData(TarEntryFormat.Pax)]
        [InlineData(TarEntryFormat.Gnu)]
        [InlineData(TarEntryFormat.Ustar)]
        [InlineData(TarEntryFormat.V7)]
        public void CanTarDirectoryWithFormat(TarEntryFormat format)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "3F6D2F2E3C1A4B5C8D9E0F1A2B3C4D5E.txt", "content");

                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,
                    Format = format,
                    DestinationFile = new FileInfo(tarFilePath),
                    SourceDirectory = new DirectoryInfo(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                GetTarEntryFormats(tarFilePath)
                    .ShouldAllBe(entryFormat => entryFormat == format, _mockEngine.Log);

                GetTarEntryNames(tarFilePath, TarDirectory.TarCompression.None)
                    .ShouldBe(["3F6D2F2E3C1A4B5C8D9E0F1A2B3C4D5E.txt"]);
            }
        }

        [Fact]
        public void UnknownFormatFallsBackToPax()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "1A2B3C4D5E6F70819AABBCCDDEEFF001.txt", "content");

                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,

                    // TarEntryFormat.Unknown is not a real archive format; the task falls back to the Pax default.
                    Format = TarEntryFormat.Unknown,
                    DestinationFile = new FileInfo(tarFilePath),
                    SourceDirectory = new DirectoryInfo(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                GetTarEntryFormats(tarFilePath)
                    .ShouldAllBe(entryFormat => entryFormat == TarEntryFormat.Pax, _mockEngine.Log);
            }
        }

        private static List<string> GetTarEntryNames(string tarFilePath, TarDirectory.TarCompression compression)
        {
            List<string> names = new List<string>();

            using FileStream stream = new FileStream(tarFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Wrap the file stream in a matching decompression stream, if the archive was compressed.
            Stream? decompressionStream = compression switch
            {
                TarDirectory.TarCompression.None => null,
                TarDirectory.TarCompression.GZip => new GZipStream(stream, CompressionMode.Decompress),
                TarDirectory.TarCompression.ZStandard => new ZstandardStream(stream, CompressionMode.Decompress),
                _ => throw new ArgumentException($"Unexpected compression '{compression}'.", nameof(compression)),
            };

            using (decompressionStream)
            {
                using TarReader reader = new TarReader(decompressionStream ?? stream);
                for (TarEntry? entry = reader.GetNextEntry(); entry is not null; entry = reader.GetNextEntry())
                {
                    if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
                    {
                        names.Add(entry.Name);
                    }
                }
            }

            return names;
        }

        private static List<TarEntryFormat> GetTarEntryFormats(string tarFilePath)
        {
            List<TarEntryFormat> formats = new List<TarEntryFormat>();

            using FileStream stream = new FileStream(tarFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using TarReader reader = new TarReader(stream);
            for (TarEntry? entry = reader.GetNextEntry(); entry is not null; entry = reader.GetNextEntry())
            {
                formats.Add(entry.Format);
            }

            return formats;
        }
    }
}

#endif
