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
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class TarDirectory_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Theory]
        [InlineData(null)]
        [InlineData("None")]
        [InlineData("GZip")]
        [InlineData("gz")]
        public void CanTarDirectory(string? compression)
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
                    DestinationFile = new TaskItem(tarFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(tarFilePath, customMessage: _mockEngine.Log);

                // Should not contain any warnings in the TarDirectory bucket (MSB4321 - MSB4330).
                _mockEngine.Log.ShouldNotContain("MSB432", customMessage: _mockEngine.Log);

                bool isGZip = compression is not null && !StringComparer.OrdinalIgnoreCase.Equals(compression, "None");

                GetTarEntryNames(tarFilePath, isGZip)
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
                    DestinationFile = new TaskItem(file.Path),
                    Overwrite = true,
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(file.Path, customMessage: _mockEngine.Log);

                GetTarEntryNames(file.Path, isGZip: false)
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
                    DestinationFile = new TaskItem(file.Path),
                    SourceDirectory = new TaskItem(folder.Path),
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
                DestinationFile = new TaskItem(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "test.tar")),
                SourceDirectory = new TaskItem(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB4321", customMessage: _mockEngine.Log);
        }

        [Fact]
        public void LogsErrorForInvalidCompression()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "9E0A4E0F5C8D4F0FA0B33F79C2F0B0C1.txt", "content");

                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,
                    Compression = "RandomUnsupportedValue",
                    DestinationFile = new TaskItem(tarFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                // Invalid compression is an error and no archive is created.
                tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4324", customMessage: _mockEngine.Log);

                File.Exists(tarFilePath).ShouldBeFalse(_mockEngine.Log);
            }
        }

        [Theory]
        [InlineData("Pax", TarEntryFormat.Pax)]
        [InlineData("gnu", TarEntryFormat.Gnu)]
        [InlineData("Ustar", TarEntryFormat.Ustar)]
        [InlineData("V7", TarEntryFormat.V7)]
        public void CanTarDirectoryWithFormat(string format, TarEntryFormat expectedFormat)
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
                    DestinationFile = new TaskItem(tarFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                GetTarEntryFormats(tarFilePath)
                    .ShouldAllBe(entryFormat => entryFormat == expectedFormat, _mockEngine.Log);

                GetTarEntryNames(tarFilePath, isGZip: false)
                    .ShouldBe(["3F6D2F2E3C1A4B5C8D9E0F1A2B3C4D5E.txt"]);
            }
        }

        [Fact]
        public void LogsErrorForInvalidFormat()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "8B0F5A2D6E1C4F0FB1C44F80D3F1C1D2.txt", "content");

                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,
                    Format = "RandomUnsupportedValue",
                    DestinationFile = new TaskItem(tarFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                // Invalid format is an error and no archive is created.
                tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4325", customMessage: _mockEngine.Log);

                File.Exists(tarFilePath).ShouldBeFalse(_mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorForUnknownFormat()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "1A2B3C4D5E6F70819AABBCCDDEEFF001.txt", "content");

                string tarFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.tar");

                TarDirectory tarDirectory = new TarDirectory
                {
                    BuildEngine = _mockEngine,

                    // "Unknown" parses to TarEntryFormat.Unknown, which is not a valid archive format.
                    Format = "Unknown",
                    DestinationFile = new TaskItem(tarFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                };

                tarDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB4325", customMessage: _mockEngine.Log);

                File.Exists(tarFilePath).ShouldBeFalse(_mockEngine.Log);
            }
        }

        private static List<string> GetTarEntryNames(string tarFilePath, bool isGZip)
        {
            List<string> names = new List<string>();

            using FileStream stream = new FileStream(tarFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Stream tarStream = isGZip ? new GZipStream(stream, CompressionMode.Decompress) : stream;

            try
            {
                using TarReader reader = new TarReader(tarStream);
                for (TarEntry? entry = reader.GetNextEntry(); entry is not null; entry = reader.GetNextEntry())
                {
                    if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
                    {
                        names.Add(entry.Name);
                    }
                }
            }
            finally
            {
                if (isGZip)
                {
                    tarStream.Dispose();
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
