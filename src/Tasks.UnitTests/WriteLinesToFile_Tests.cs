// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

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

        [Fact]
        public void RedundantParametersAreLogged()
        {
            using TestEnvironment testEnv = TestEnvironment.Create(_output);

            MockEngine engine = new(_output);

            string file = testEnv.ExpectFile().Path;

            WriteLinesToFile task = new()
            {
                BuildEngine = engine,
                File = new TaskItem(file),
                Lines = new ITaskItem[] { new TaskItem($"{nameof(RedundantParametersAreLogged)} Test") },
                WriteOnlyWhenDifferent = true,
                Overwrite = false,
            };

            task.Execute().ShouldBeTrue();
            engine.AssertLogContainsMessageFromResource(AssemblyResources.GetString, "WriteLinesToFile.UnusedWriteOnlyWhenDifferent", file);
        }

        /// <summary>
        /// Question WriteLines to return false when a write will be required.
        /// </summary>
        [Fact]
        public void QuestionWriteLinesWriteOnlyWhenDifferentTest()
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
                    Lines = new ITaskItem[] { new TaskItem("File contents1") },
                    FailIfNotIncremental = true,
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
                    Lines = new ITaskItem[] { new TaskItem("File contents2") },
                    FailIfNotIncremental = true,
                };
                a3.Execute().ShouldBeFalse();
                File.GetLastWriteTime(file).ShouldBe(writeTime, tolerance: TimeSpan.FromSeconds(1));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Question WriteLines to return true when Lines are empty.
        /// </summary>
        [Fact]
        public void QuestionWriteLinesWhenLinesAreEmpty()
        {
            // Test the combination of:
            // 1) File exists
            // 2) Overwrite
            // 3) WriteOnlyWhenDifferent

            var fileExists = FileUtilities.GetTemporaryFile();
            var fileNotExists = FileUtilities.GetTemporaryFileName();
            try
            {
                TestWriteLines(fileExists, fileNotExists, Overwrite: true, WriteOnlyWhenDifferent: true);
                TestWriteLines(fileExists, fileNotExists, Overwrite: false, WriteOnlyWhenDifferent: true);
                TestWriteLines(fileExists, fileNotExists, Overwrite: true, WriteOnlyWhenDifferent: false);
                TestWriteLines(fileExists, fileNotExists, Overwrite: false, WriteOnlyWhenDifferent: false);
            }
            finally
            {
                File.Delete(fileExists);
            }

            void TestWriteLines(string fileExists, string fileNotExists, bool Overwrite, bool WriteOnlyWhenDifferent)
            {
                var test1 = new WriteLinesToFile
                {
                    Overwrite = Overwrite,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(fileExists),
                    WriteOnlyWhenDifferent = WriteOnlyWhenDifferent,
                    FailIfNotIncremental = true,
                    // Tests Lines = null.
                };
                test1.Execute().ShouldBeTrue();

                var test2 = new WriteLinesToFile
                {
                    Overwrite = Overwrite,
                    BuildEngine = new MockEngine(_output),
                    File = new TaskItem(fileNotExists),
                    WriteOnlyWhenDifferent = WriteOnlyWhenDifferent,
                    FailIfNotIncremental = true,
                    Lines = Array.Empty<ITaskItem>(),  // Test empty.
                };
                test2.Execute().ShouldBeTrue();
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
        public void WritingNothingErasesExistingFile(bool useNullLines)
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
        public void WritingNothingCreatesNewFile(bool useNullLines)
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

        [Fact]
        public void TransactionalModeHandlesConcurrentWritesSuccessfully()
        {
            using (var testEnv = TestEnvironment.Create(_output))
            {
                var outputFile = testEnv.CreateFile("output.txt").Path;
                var projectCount = 8;

                // Create parent project file to run child projects in parallel
                var parallelProjectContent = @$"
            <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <ItemGroup>
            {string.Join("\n", Enumerable.Range(1, projectCount).Select(i => $@"<Project Include=""TestProject{i}.csproj"" />"))}
                </ItemGroup>
                <Target Name=""Build"">
                <MSBuild Projects=""@(Project)"" Targets=""WriteToFile"" BuildInParallel=""true""/>
                </Target>
            </Project>";
                var parallelProjectFile = testEnv.CreateFile("ParallelBuildProject.csproj", parallelProjectContent).Path;

                // Create child project instances
                for (int i = 0; i < projectCount; i++)
                {
                    var projectContent = @$"
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>
                    <LinesToWrite Include=""Line from Test{i + 1} at $([System.DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))"" />
                    </ItemGroup>
                    <Target Name=""WriteToFile"">
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    </Target>
                </Project>";
                    testEnv.CreateFile($"TestProject{i + 1}.csproj", projectContent);
                }

                // Create and run the parent project
                var parallelProject = new ProjectInstance(parallelProjectFile);
                var buildManager = BuildManager.DefaultBuildManager;

                var buildParameters = new BuildParameters
                {
                    MaxNodeCount = Environment.ProcessorCount
                };

                var buildRequestData = new BuildRequestData(parallelProject, new[] { "Build" }, null);
                var buildResult = buildManager.Build(buildParameters, buildRequestData);

                // Verify build result
                buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

                // Verify output file exists and contains content from one of the projects
                File.Exists(outputFile).ShouldBeTrue();
                var content = File.ReadAllText(outputFile);
                content.ShouldNotBeEmpty();
                content.ShouldMatch(@"Line from Test\d+ at \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}" + Environment.NewLine);
            }
        }

        [Fact]
        public void TransactionalModePreservesAllData()
        {
            using (var testEnv = TestEnvironment.Create(_output))
            {
                var outputFile = testEnv.CreateFile("output.txt").Path;
                var projectCount = 8;

                var parallelProjectContent = @$"
            <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <ItemGroup>
            {string.Join("\n", Enumerable.Range(1, projectCount).Select(i => $@"<Project Include=""TestProject{i}.csproj"" />"))}
                </ItemGroup>
                <Target Name=""Build"">
                <MSBuild Projects=""@(Project)"" Targets=""WriteToFile"" BuildInParallel=""true""/>
                </Target>
            </Project>";
                var parallelProjectFile = testEnv.CreateFile("ParallelBuildProject.csproj", parallelProjectContent).Path;

                for (int i = 0; i < projectCount; i++)
                {
                    var projectContent = @$"
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>
                    <LinesToWrite Include=""Line from Project {i + 1}"" />
                    </ItemGroup>
                    <Target Name=""WriteToFile"">
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Transactional=""true""/>
                    </Target>
                </Project>";
                    testEnv.CreateFile($"TestProject{i + 1}.csproj", projectContent);
                }

                var parallelProject = new ProjectInstance(parallelProjectFile);
                var buildManager = BuildManager.DefaultBuildManager;
                var buildParameters = new BuildParameters { MaxNodeCount = Environment.ProcessorCount };
                var buildRequestData = new BuildRequestData(parallelProject, new[] { "Build" }, null);
                var buildResult = buildManager.Build(buildParameters, buildRequestData);

                // Verify build succeeded
                buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

                // Verify ALL data is preserved (no data loss)
                var content = File.ReadAllText(outputFile);
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Expected: 8 projects × 3 writes = 24 lines
                var expectedLines = projectCount * 3;
                lines.Length.ShouldBe(expectedLines,
                    $"Transactional mode should preserve all {expectedLines} lines");

                // Verify each project's output appears exactly 3 times
                for (int i = 1; i <= projectCount; i++)
                {
                    var count = lines.Count(line => line.Contains($"Line from Project {i}"));
                    count.ShouldBe(3, $"Project {i} should have 3 lines");
                }
            }
        }

        [Fact]
        public void NonTransactionalModeCausesDataLoss()
        {
            using (var testEnv = TestEnvironment.Create(_output))
            {
                var outputFile = testEnv.CreateFile("output.txt").Path;
                var projectCount = 20; 

                var parallelProjectContent = @$"
            <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <ItemGroup>
            {string.Join("\n", Enumerable.Range(1, projectCount).Select(i => $@"<Project Include=""TestProject{i}.csproj"" />"))}
                </ItemGroup>
                <Target Name=""Build"">
                <MSBuild Projects=""@(Project)"" Targets=""WriteToFile"" BuildInParallel=""true""/>
                </Target>
            </Project>";
                var parallelProjectFile = testEnv.CreateFile("ParallelBuildProject.csproj", parallelProjectContent).Path;

                for (int i = 0; i < projectCount; i++)
                {
                    var projectContent = @$"
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>
                    <LinesToWrite Include=""Line from Project {i + 1}"" />
                    </ItemGroup>
                    <Target Name=""WriteToFile"">
                    <!-- NO Transactional mode, Overwrite=true -->
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Overwrite=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Overwrite=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Overwrite=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Overwrite=""true""/>
                    <WriteLinesToFile File=""{outputFile}"" Lines=""@(LinesToWrite)"" Overwrite=""true""/>
                    </Target>
                </Project>";
                    testEnv.CreateFile($"TestProject{i + 1}.csproj", projectContent);
                }

                var parallelProject = new ProjectInstance(parallelProjectFile);
                var buildManager = BuildManager.DefaultBuildManager;
                var buildParameters = new BuildParameters { MaxNodeCount = Environment.ProcessorCount };
                var buildRequestData = new BuildRequestData(parallelProject, new[] { "Build" }, null);
                var buildResult = buildManager.Build(buildParameters, buildRequestData);

                var content = File.ReadAllText(outputFile);
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                var expectedWithoutRace = projectCount * 5;

                lines.Length.ShouldBeLessThan(expectedWithoutRace,
                    $"Without transactional mode, data loss should occur. " +
                    $"Expected significant data loss from {expectedWithoutRace} lines, but got {lines.Length}");

                lines.Length.ShouldBeLessThanOrEqualTo(5,
                    "With Overwrite=true and parallel builds, only last project's writes should survive");
            }
        }
    }
}
