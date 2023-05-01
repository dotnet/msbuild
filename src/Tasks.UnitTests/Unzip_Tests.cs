// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

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

                unzip.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("638AF4AE88A146E09CB69FE1CA7083DC", customMessage: _mockEngine.Log);
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

                // Question new task, should be false.
                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    FailIfNotIncremental = true,
                };
                unzip.Execute().ShouldBeFalse(_mockEngine.Log);
                _mockEngine.Log = string.Empty;

                // Run the task.
                Unzip unzip2 = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    FailIfNotIncremental = false,
                };
                unzip2.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "BE78A17D30144B549D21F71D5C633F7D.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "A04FF4B88DF14860B7C73A8E75A4FB76.txt"), customMessage: _mockEngine.Log);

                // Question ran task, should be true
                Unzip unzip3 = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = true,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    FailIfNotIncremental = true,
                };
                unzip3.Execute().ShouldBeTrue(_mockEngine.Log);
            }
        }

        [Fact]
        public void CanUnzip_ExplicitDirectoryEntries()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder source = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);
                testEnvironment.CreateFile(source, "BE78A17D30144B549D21F71D5C633F7D.txt", "file1");
                testEnvironment.CreateFile(source, "A04FF4B88DF14860B7C73A8E75A4FB76.txt", "file2");
                TransientTestFolder emptyDir = source.CreateDirectory("emptyDir");
                TransientTestFolder subDir = source.CreateDirectory("subDir");
                subDir.CreateFile("F83E9633685494E53BEF3794EDEEE6A6.txt", "file3");
                subDir.CreateFile("21D6D4596067723B3AC5DF9A8B3CBFE7.txt", "file4");

                TransientZipArchive zipArchive = TransientZipArchive.Create(source, testEnvironment.CreateFolder(createFolder: true));

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) }
                };

                unzip.Execute().ShouldBeTrue(customMessage: _mockEngine.Log);

                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "BE78A17D30144B549D21F71D5C633F7D.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "A04FF4B88DF14860B7C73A8E75A4FB76.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "subdir", "F83E9633685494E53BEF3794EDEEE6A6.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "subdir", "21D6D4596067723B3AC5DF9A8B3CBFE7.txt"), customMessage: _mockEngine.Log);
                Directory.Exists(Path.Combine(destination.Path, "emptyDir"));
            }
        }

        [WindowsOnlyFact(additionalMessage: "Can't figure out how to make CreateDirectory throw on non-Windows.")]
        public void LogsErrorIfDirectoryCannotBeCreated()
        {
            Unzip unzip = new Unzip
            {
                BuildEngine = _mockEngine,
                DestinationFolder = new TaskItem(String.Empty)
            };

            unzip.Execute().ShouldBeFalse(_mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB3931", customMessage: _mockEngine.Log);
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

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain($"D6DFD219DACE48F8B86EFCDF98433333.txt{(NativeMethodsShared.IsMono ? "\"" : "'")} is denied", customMessage: _mockEngine.Log);
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

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3933", customMessage: _mockEngine.Log);
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

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3932", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void CanUnzip_WithIncludeFilter()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Include = "BE78A17D30144B549D21F71D5C633F7D.txt"
                };

                unzip.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "BE78A17D30144B549D21F71D5C633F7D.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldNotContain(Path.Combine(destination.Path, "A04FF4B88DF14860B7C73A8E75A4FB76.txt"), customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void CanUnzip_WithExcludeFilter()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Exclude = "BE78A17D30144B549D21F71D5C633F7D.txt"
                };

                unzip.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldNotContain(Path.Combine(destination.Path, "BE78A17D30144B549D21F71D5C633F7D.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "A04FF4B88DF14860B7C73A8E75A4FB76.txt"), customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void CanUnzip_WithIncludeAndExcludeFilter()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder source = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder destination = testEnvironment.CreateFolder(createFolder: false);
                TransientTestFolder sub = source.CreateDirectory("sub");
                testEnvironment.CreateFile(source, "file1.js", "file1");
                testEnvironment.CreateFile(source, "file1.js.map", "file2");
                testEnvironment.CreateFile(source, "file2.js", "file3");
                testEnvironment.CreateFile(source, "readme.txt", "file4");
                testEnvironment.CreateFile(sub, "subfile.js", "File5");

                TransientZipArchive zipArchive = TransientZipArchive.Create(source, testEnvironment.CreateFolder(createFolder: true));

                Unzip unzip = new Unzip
                {
                    BuildEngine = _mockEngine,
                    DestinationFolder = new TaskItem(destination.Path),
                    OverwriteReadOnlyFiles = true,
                    SkipUnchangedFiles = false,
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Include = "*.js",
                    Exclude = "*.js.map;sub\\*.js"
                };

                unzip.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "file1.js"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldNotContain(Path.Combine(destination.Path, "file1.js.map"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(Path.Combine(destination.Path, "file2.js"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldNotContain(Path.Combine(destination.Path, "readme.txt"), customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldNotContain(Path.Combine(destination.Path, "sub", "subfile.js"), customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfIncludeContainsInvalidPathCharacters()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Include = "<BE78A17D30144B|549D21F71D5C633F7D/.txt"
                };

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3937", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfIncludeContainsPropertyReferences()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Include = "$(Include)"
                };

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3938", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfExcludeContainsInvalidPathCharacters()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Exclude = "<BE78A17D30144B|549D21F71D5C633F7D/.txt"
                };

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3937", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfExcludeContainsPropertyReferences()
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
                    SourceFiles = new ITaskItem[] { new TaskItem(zipArchive.Path) },
                    Exclude = "$(Include)"
                };

                unzip.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3938", customMessage: _mockEngine.Log);
            }
        }
    }
}
