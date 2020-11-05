using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public sealed class WriteLinesToFile_Tests
    {
        private readonly ITestOutputHelper _output;

        public WriteLinesToFile_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Invalid encoding
        /// </summary>
        [Fact]
        public void InvalidEncoding()
        {
            var a = new WriteLinesToFile
            {
                BuildEngine = new MockEngine(_output),
                Encoding = "||invalid||",
                File = new TaskItem("c:\\" + Guid.NewGuid().ToString()),
                Lines = new TaskItem[] { new TaskItem("x") }
            };

            Assert.False(a.Execute());
            ((MockEngine)a.BuildEngine).AssertLogContains("MSB3098");
            Assert.False(File.Exists(a.File.ItemSpec));
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [Fact]
        public void Encoding()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Write default encoding: UTF8
                var a = new WriteLinesToFile
                {
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("\uBDEA") }
                };
                Assert.True(a.Execute());

                var r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.Equal("\uBDEA", r.Lines[0].ItemSpec);

                File.Delete(file);

                // Write ANSI .. that won't work! 
                a = new WriteLinesToFile
                {
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("\uBDEA") },
                    Encoding = "ASCII"
                };
                Assert.True(a.Execute());

                // Read the line from the file.
                r = new ReadLinesFromFile
                {
                    File = new TaskItem(file)
                };
                Assert.True(r.Execute());

                Assert.NotEqual("\uBDEA", r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void WriteLinesWriteOnlyWhenDifferentTest()
        {
            var file = FileUtilities.GetTemporaryFile();
            try
            {
                // Write an initial file.
                var a = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] { new TaskItem("File contents1") }
                };

                a.Execute().ShouldBeTrue();

                // Verify contents
                var r = new ReadLinesFromFile { File = new TaskItem(file) };
                r.Execute().ShouldBeTrue();
                r.Lines[0].ItemSpec.ShouldBe("File contents1");

                var writeTime = DateTime.Now.AddHours(-1);

                File.SetLastWriteTime(file, writeTime);

                // Write the same contents to the file, timestamps should match.
                var a2 = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] { new TaskItem("File contents1") }
                };
                a2.Execute().ShouldBeTrue();
                File.GetLastWriteTime(file).ShouldBe(writeTime, tolerance: TimeSpan.FromSeconds(1));

                // Write different contents to the file, last write time should differ.
                var a3 = new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    WriteOnlyWhenDifferent = true,
                    Lines = new ITaskItem[] { new TaskItem("File contents2") }
                };

                a3.Execute().ShouldBeTrue();
                File.GetLastWriteTime(file).ShouldBeGreaterThan(writeTime.AddSeconds(1));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Should create directory structure when target <see cref="WriteLinesToFile.File"/> does not exist.
        /// </summary>
        [Fact]
        public void WriteLinesToFileDoesCreateDirectory()
        {
            using (var testEnv = TestEnvironment.Create())
            {
                var directory = testEnv.CreateFolder(folderPath: null, createFolder: false);
                var file = Path.Combine(directory.Path, $"{Guid.NewGuid().ToString("N")}.tmp");

                var WriteLinesToFile = new WriteLinesToFile
                {
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file),
                    Lines = new ITaskItem[] { new TaskItem("WriteLinesToFileDoesCreateDirectory Test") }
                };

                // Verify that the diretory doesn't exist. Otherwise the test would pass - even it should not.
                Directory.Exists(directory.Path).ShouldBeFalse();

                WriteLinesToFile.Execute().ShouldBeTrue();

                Directory.Exists(directory.Path).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private void WritingNothingErasesExistingFile(bool useNullLines)
        {
            ITaskItem[] lines = useNullLines ? null : Array.Empty<ITaskItem>();

            using (var testEnv = TestEnvironment.Create())
            {
                var file = testEnv.CreateFile("FileToBeEmptied.txt", "Contents that should be erased");

                File.Exists(file.Path).ShouldBeTrue();
                File.ReadAllText(file.Path).ShouldNotBeEmpty();

                new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file.Path),
                    Lines = lines
                }.Execute().ShouldBeTrue();

                File.Exists(file.Path).ShouldBeTrue();
                File.ReadAllText(file.Path).ShouldBeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private void WritingNothingCreatesNewFile(bool useNullLines)
        {
            ITaskItem[] lines = useNullLines ? null : Array.Empty<ITaskItem>();

            using (var testEnv = TestEnvironment.Create())
            {
                var file = testEnv.GetTempFile();

                File.Exists(file.Path).ShouldBeFalse();

                new WriteLinesToFile
                {
                    Overwrite = true,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(file.Path),
                    Lines = lines
                }.Execute().ShouldBeTrue();

                File.Exists(file.Path).ShouldBeTrue();
                File.ReadAllText(file.Path).ShouldBeEmpty();
            }
        }
    }
}
