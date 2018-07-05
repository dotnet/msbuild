// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class ZipDirectory_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        [Fact]
        public void CanZipDirectory()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "6DE6060259C44DB6B145159376751C22.txt", "6DE6060259C44DB6B145159376751C22");
                testEnvironment.CreateFile(sourceFolder, "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt", "CDA3DD8C25A54A7CAC638A444CB1EAD0");

                string zipFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.zip");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new TaskItem(zipFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path)
                };

                zipDirectory.Execute().ShouldBeTrue(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, () => _mockEngine.Log);
                _mockEngine.Log.ShouldContain(zipFilePath, () => _mockEngine.Log);

                using (FileStream stream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    archive.Entries
                        .Select(i => i.FullName)
                        .ToList()
                        .ShouldBe(
                            new List<string>
                            {
                                "6DE6060259C44DB6B145159376751C22.txt",
                                "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt"
                            },
                            ignoreOrder: true);
                }
            }
        }

        [Fact]
        public void CanOvewriteExistingFile()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "F1C22D660B0D4DAAA296C1B980320B03.txt", "F1C22D660B0D4DAAA296C1B980320B03");
                testEnvironment.CreateFile(sourceFolder, "AA825D1CB154492BAA58E1002CE1DFEB.txt", "AA825D1CB154492BAA58E1002CE1DFEB");

                TransientTestFile file = testEnvironment.CreateFile(testEnvironment.DefaultTestDirectory, "test.zip", contents: "test");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new TaskItem(file.Path),
                    Overwrite = true,
                    SourceDirectory = new TaskItem(sourceFolder.Path)
                };

                zipDirectory.Execute().ShouldBeTrue(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, () => _mockEngine.Log);
                _mockEngine.Log.ShouldContain(file.Path, () => _mockEngine.Log);

                using (FileStream stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    archive.Entries
                        .Select(i => i.FullName)
                        .ToList()
                        .ShouldBe(
                            new List<string>
                            {
                                "F1C22D660B0D4DAAA296C1B980320B03.txt",
                                "AA825D1CB154492BAA58E1002CE1DFEB.txt"
                            },
                            ignoreOrder: true);
                }
            }
        }

        [Fact]
        public void LogsErrorIfDestinationExists()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);

                TransientTestFile file = testEnvironment.CreateFile("foo.zip", "foo");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new TaskItem(file.Path),
                    SourceDirectory = new TaskItem(folder.Path)
                };

                zipDirectory.Execute().ShouldBeFalse(() => _mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3942", () => _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfDirectoryDoesNotExist()
        {
            ZipDirectory zipDirectory = new ZipDirectory
            {
                BuildEngine = _mockEngine,
                SourceDirectory = new TaskItem(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            };

            zipDirectory.Execute().ShouldBeFalse(() => _mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB3941", () => _mockEngine.Log);
        }
    }
}
