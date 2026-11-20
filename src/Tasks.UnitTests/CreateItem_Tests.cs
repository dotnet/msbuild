// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Task = System.Threading.Tasks.Task;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class CreateItem_Tests : IDisposable
    {
        internal const string CreateItemWithInclude = @"
            <Project>
                <Target Name='TestTarget' Returns='@(Text)'>
                    <CreateItem Include='{0}'>
                        <Output TaskParameter='Include' ItemName='Text'/>
                    </CreateItem>
                </Target>
            </Project>
            ";

        private readonly ITestOutputHelper _testOutput;
        private Lazy<DummyMappedDrive> _mappedDrive = DummyMappedDriveUtils.GetLazyDummyMappedDrive();

        public void Dispose()
        {
            _mappedDrive.Value?.Dispose();
        }

        public CreateItem_Tests(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        /// <summary>
        /// CreateIteming identical lists results in empty list.
        /// </summary>
        [Fact]
        public void OneFromOneIsZero()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Include = new ITaskItem[] { new TaskItem("MyFile.txt") };
            t.Exclude = new ITaskItem[] { new TaskItem("MyFile.txt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Empty(t.Include);
        }

        /// <summary>
        /// CreateIteming completely different lists results in left list.
        /// </summary>
        [Fact]
        public void OneFromOneMismatchIsOne()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Include = new ITaskItem[] { new TaskItem("MyFile.txt") };
            t.Exclude = new ITaskItem[] { new TaskItem("MyFileOther.txt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Single(t.Include);
            Assert.Equal("MyFile.txt", t.Include[0].ItemSpec);
        }

        /// <summary>
        /// If 'Exclude' is unspecified, then 'Include' is the result.
        /// </summary>
        [Fact]
        public void UnspecifiedFromOneIsOne()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Include = new ITaskItem[] { new TaskItem("MyFile.txt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Single(t.Include);
            Assert.Equal(t.Include[0].ItemSpec, t.Include[0].ItemSpec);
        }


        /// <summary>
        /// If 'Include' is unspecified, then empty is the result.
        /// </summary>
        [Fact]
        public void OneFromUnspecifiedIsEmpty()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Exclude = new ITaskItem[] { new TaskItem("MyFile.txt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Empty(t.Include);
        }

        /// <summary>
        /// If 'Include' and 'Exclude' are unspecified, then empty is the result.
        /// </summary>
        [Fact]
        public void UnspecifiedFromUnspecifiedIsEmpty()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            bool success = t.Execute();

            Assert.True(success);
            Assert.Empty(t.Include);
        }


        /// <summary>
        /// CreateItem is case insensitive.
        /// </summary>
        [Fact]
        public void CaseDoesntMatter()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Include = new ITaskItem[] { new TaskItem("MyFile.txt") };
            t.Exclude = new ITaskItem[] { new TaskItem("myfile.tXt") };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Empty(t.Include);
        }

        /// <summary>
        /// Using the CreateItem task to expand wildcards, and then try accessing the RecursiveDir
        /// metadata to force batching.
        /// </summary>
        [Fact]
        public void WildcardsWithRecursiveDir()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Myapp.proj", @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name =`Repro`>
                    <CreateItem Include=`**\*.txt`>
                      <Output TaskParameter=`Include` ItemName=`Text`/>
                    </CreateItem>
                    <Copy SourceFiles=`@(Text)` DestinationFiles=`Destination\%(RecursiveDir)%(Filename)%(Extension)`/>
                  </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Foo.txt", "foo");
            ObjectModelHelpers.CreateFileInTempProjectDirectory(Path.Combine("Subdir", "Bar.txt"), "bar");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("Myapp.proj", logger);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(Path.Combine("Destination", "Foo.txt"));
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(Path.Combine("Destination", "Subdir", "Bar.txt"));
        }

        /// <summary>
        /// Using the CreateItem task to expand wildcards and verifying that the RecursiveDir metadatum is successfully
        /// serialized/deserialized cross process.
        /// </summary>
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/14429")]
        [Fact]
        public void RecursiveDirOutOfProc()
        {
            using var env = TestEnvironment.Create(_testOutput);

            ObjectModelHelpers.DeleteTempProjectDirectory();

            string projectFileFullPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("Myapp.proj", @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name =`Repro` Returns=`@(Text)`>
                    <CreateItem Include=`**\*.txt`>
                      <Output TaskParameter=`Include` ItemName=`Text`/>
                    </CreateItem>
                  </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(Path.Combine("Subdir", "Bar.txt"), "bar");

            BuildRequestData data = new BuildRequestData(projectFileFullPath, new Dictionary<string, string>(), null, new string[] { "Repro" }, null);
            BuildParameters parameters = new BuildParameters
            {
                DisableInProcNode = true,
                EnableNodeReuse = false,
                Loggers = new ILogger[] { new MockLogger(_testOutput) },
            };
            BuildResult result = BuildManager.DefaultBuildManager.Build(parameters, data);
            result.OverallResult.ShouldBe(BuildResultCode.Success);
            result.ResultsByTarget["Repro"].Items[0].GetMetadata("RecursiveDir").ShouldBe("Subdir" + Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// CreateItem should add additional metadata when instructed
        /// </summary>
        [Fact]
        public void AdditionalMetaData()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            t.Include = new ITaskItem[] { new TaskItem("MyFile.txt") };
            t.AdditionalMetadata = new string[] { "MyMetaData=SomeValue" };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal("SomeValue", t.Include[0].GetMetadata("MyMetaData"));
        }

        /// <summary>
        /// We should be able to preserve the existing metadata on items
        /// </summary>
        [Fact]
        public void AdditionalMetaDataPreserveExisting()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            TaskItem item = new TaskItem("MyFile.txt");
            item.SetMetadata("MyMetaData", "SomePreserveMeValue");

            t.Include = new ITaskItem[] { item };
            t.PreserveExistingMetadata = true;

            t.AdditionalMetadata = new string[] { "MyMetaData=SomeValue" };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal("SomePreserveMeValue", t.Include[0].GetMetadata("MyMetaData"));
        }

        /// <summary>
        /// The default is to overwrite existing metadata on items
        /// </summary>
        [Fact]
        public void AdditionalMetaDataOverwriteExisting()
        {
            CreateItem t = new CreateItem();
            t.BuildEngine = new MockEngine();

            TaskItem item = new TaskItem("MyFile.txt");
            item.SetMetadata("MyMetaData", "SomePreserveMeValue");

            t.Include = new ITaskItem[] { item };

            // The default for CreateItem is to overwrite any existing metadata
            // t.PreserveExistingMetadata = false;

            t.AdditionalMetadata = new string[] { "MyMetaData=SomeOverwriteValue" };

            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal("SomeOverwriteValue", t.Include[0].GetMetadata("MyMetaData"));
        }

        /// <summary>
        /// Logs error when encountering wildcard drive enumeration during task item creation.
        /// </summary>
        [Theory]
        [InlineData(@"/**")]
        [InlineData(@"/**/*.cs")]
        [InlineData(@"/**/*/*.cs")]
        public void WildcardDriveEnumerationTaskItemLogsError(string itemSpec)
        {
            using (var env = TestEnvironment.Create())
            {
                Helpers.ResetStateForDriveEnumeratingWildcardTests(env, "1");

                try
                {
                    MockEngine engine = new MockEngine();
                    CreateItem t = new CreateItem()
                    {
                        BuildEngine = engine,
                        Include = new ITaskItem[] { new TaskItem(itemSpec) },
                    };

                    t.Execute().ShouldBeFalse();
                    engine.Errors.ShouldBe(1);
                    engine.AssertLogContains("MSB5029");
                    engine.AssertLogContains(engine.ProjectFileOfTaskNode);
                }
                finally
                {
                    ChangeWaves.ResetStateForTests();
                }
            }
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during task item creation on Windows platform.
        /// </summary>
        [WindowsOnlyTheory]
        [InlineData(@"%DRIVE%:\**")]
        [InlineData(@"%DRIVE%:\**\*.log")]
        [InlineData(@"%DRIVE%:\\\\**\*.log")]
        public void LogWindowsWarningUponCreateItemExecution(string itemSpec)
        {
            itemSpec = DummyMappedDriveUtils.UpdatePathToMappedDrive(itemSpec, _mappedDrive.Value.MappedDriveLetter);
            VerifyDriveEnumerationWarningLoggedUponCreateItemExecution(itemSpec);
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during task item creation on Unix platform.
        /// </summary>
        [UnixOnlyTheory]
        [InlineData(@"\**")]
        [InlineData(@"\**\*.log")]
        public void LogUnixWarningUponCreateItemExecution(string itemSpec)
        {
            VerifyDriveEnumerationWarningLoggedUponCreateItemExecution(itemSpec);
        }

        private static void VerifyDriveEnumerationWarningLoggedUponCreateItemExecution(string itemSpec)
        {
            using (var env = TestEnvironment.Create())
            {
                Helpers.ResetStateForDriveEnumeratingWildcardTests(env, "0");

                try
                {
                    MockEngine engine = new MockEngine();
                    CreateItem t = new CreateItem()
                    {
                        BuildEngine = engine,
                        Include = new ITaskItem[] { new TaskItem(itemSpec) },
                    };

                    t.Execute().ShouldBeTrue();
                    engine.Warnings.ShouldBe(1);
                    engine.AssertLogContains("MSB5029");
                    engine.AssertLogContains(engine.ProjectFileOfTaskNode);
                }
                finally
                {
                    ChangeWaves.ResetStateForTests();
                }
            }
        }

        /// <summary>
        /// Throws exception when encountering wildcard drive enumeration during CreateItem task execution.
        /// </summary>
        [Theory]
        [InlineData(
            CreateItemWithInclude,
            @"\**")]

        [InlineData(
            CreateItemWithInclude,
            @"\**\*.txt")]

        [InlineData(
            CreateItemWithInclude,
            @"$(empty)\**\*.cs")]
        public void ThrowExceptionUponItemCreationWithDriveEnumeration(string content, string include)
        {
            content = string.Format(content, include);
            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                content,
                "1",
                "TestTarget",
                Helpers.ExpectedBuildResult.FailWithError,
                _testOutput);
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during CreateItem task execution on Windows platform.
        /// </summary>
        [WindowsOnlyTheory]
        [InlineData(
            CreateItemWithInclude,
            @"%DRIVE%:\**")]

        [InlineData(
            CreateItemWithInclude,
            @"%DRIVE%:\**\*.txt")]

        [InlineData(
            CreateItemWithInclude,
            @"%DRIVE%:$(empty)\**\*.cs")]
        public void LogWindowsWarningUponItemCreationWithDriveEnumeration(string content, string include)
        {
            include = DummyMappedDriveUtils.UpdatePathToMappedDrive(include, _mappedDrive.Value.MappedDriveLetter);
            content = string.Format(content, include);
            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                content,
                "0",
                "TestTarget",
                Helpers.ExpectedBuildResult.SucceedWithWarning,
                _testOutput);
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during CreateItem task execution on Unix platform.
        /// </summary>
        [UnixOnlyTheory]
        [InlineData(
            CreateItemWithInclude,
            @"\**")]

        [InlineData(
            CreateItemWithInclude,
            @"\**\*.txt")]

        [InlineData(
            CreateItemWithInclude,
            @"$(empty)\**\*.cs")]
        public void LogUnixWarningUponItemCreationWithDriveEnumeration(string content, string include)
        {
            content = string.Format(content, include);
            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                    content,
                    "0",
                    "TestTarget",
                    Helpers.ExpectedBuildResult.SucceedWithWarning,
                    _testOutput);
        }

        /// <summary>
        /// Relative wildcard Include resolves against TaskEnvironment.ProjectDirectory, not cwd.
        /// </summary>
        [Fact]
        public void WildcardInclude_ResolvesAgainstProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);

            TransientTestFolder projectDir = env.CreateFolder(createFolder: true);
            env.CreateFile(projectDir, "alpha.txt", "alpha");

            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.CreateFile(otherDir, "beta.txt", "beta");

            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = [new TaskItem("*.txt")],
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            t.Include[0].ItemSpec.ShouldBe("alpha.txt");
        }

        /// <summary>
        /// Relative wildcard Exclude resolves against TaskEnvironment.ProjectDirectory, not cwd.
        /// The *.log exclude should match SampleFile.log in the project directory and filter it out.
        /// </summary>
        [Fact]
        public void WildcardExclude_ResolvesAgainstProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);

            TransientTestFolder projectDir = env.CreateFolder(createFolder: true);
            env.CreateFile(projectDir, "SampleFile.log", "SampleFile");

            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.CreateFile(otherDir, "decoy.log", "decoy");
            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = [new TaskItem("SampleFile.log")],
                Exclude = [new TaskItem("*.log")],
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.ShouldBeEmpty();
        }

        /// <summary>
        /// RecursiveDir metadata is correctly set when wildcard expansion uses a custom ProjectDirectory.
        /// </summary>
        [Fact]
        public void RecursiveDir_WithCustomProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);

            TransientTestFolder projectDir = env.CreateFolder(createFolder: true);
            string sampleSubdirectory = Path.Combine(projectDir.Path, "SampleSubdirectory");
            Directory.CreateDirectory(sampleSubdirectory);
            File.WriteAllText(Path.Combine(sampleSubdirectory, "SampleFile.txt"), "content");

            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = [new TaskItem($"**{Path.DirectorySeparatorChar}*.txt")],
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            t.Include[0].ItemSpec.ShouldBe(Path.Combine("SampleSubdirectory", "SampleFile.txt"));

            string recursiveDir = t.Include[0].GetMetadata("RecursiveDir");
            recursiveDir.ShouldBe("SampleSubdirectory" + Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Absolute wildcard patterns are unaffected by ProjectDirectory — the same files are returned
        /// regardless of what ProjectDirectory is set to.
        /// </summary>
        [Fact]
        public void AbsoluteWildcard_IgnoresProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);

            TransientTestFolder targetDir = env.CreateFolder(createFolder: true);
            env.CreateFile(targetDir, "SampleFile.txt", "content");

            TransientTestFolder unrelatedDir = env.CreateFolder(createFolder: true);

            string absolutePattern = Path.Combine(targetDir.Path, "*.txt");
            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = [new TaskItem(absolutePattern)],
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(unrelatedDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            t.Include[0].ItemSpec.ShouldBe(Path.Combine(targetDir.Path, "SampleFile.txt"));
        }

        [Fact]
        public async Task ConcurrentProjectDirectories_ReturnDisjointFileSets()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder firstDirectory = env.CreateFolder(createFolder: true);
            TransientTestFolder secondDirectory = env.CreateFolder(createFolder: true);
            TransientTestFolder decoyDirectory = env.CreateFolder(createFolder: true);
            env.CreateFile(firstDirectory, "first.txt", "first");
            env.CreateFile(secondDirectory, "second.txt", "second");
            env.CreateFile(decoyDirectory, "decoy.txt", "decoy");
            env.SetCurrentDirectory(decoyDirectory.Path);

            CreateItem first = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(firstDirectory.Path),
                Include = [new TaskItem("*.txt")],
            };
            CreateItem second = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(secondDirectory.Path),
                Include = [new TaskItem("*.txt")],
            };

            bool[] results = await Task.WhenAll(
                Task.Run(first.Execute),
                Task.Run(second.Execute));

            results.ShouldBe([true, true]);
            first.Include.ShouldHaveSingleItem().ItemSpec.ShouldBe("first.txt");
            second.Include.ShouldHaveSingleItem().ItemSpec.ShouldBe("second.txt");
        }

        [Theory]
        [InlineData("*.txt", "root.txt", "")]
        [InlineData("./*.txt", "./root.txt", "")]
        [InlineData("sub/../*.txt", "sub/../root.txt", "")]
        [InlineData("sub/**/*.txt", "sub/nested/child.txt", "nested/")]
        [InlineData("missing/*.txt", null, null)]
        [InlineData("invalid.../*.txt", null, null)]
        public void FallbackTaskEnvironment_WildcardParityWithLegacy(string pattern, string expectedItem, string expectedRecursiveDir)
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder directory = env.CreateFolder(createFolder: true);
            env.CreateFile(directory, "root.txt", "root");
            string nestedDirectory = Path.Combine(directory.Path, "sub", "nested");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(nestedDirectory, "child.txt"), "child");
            env.SetCurrentDirectory(directory.Path);

            CreateItem task = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = [new TaskItem(pattern)],
            };

            task.Execute().ShouldBeTrue();

            if (expectedItem is null)
            {
                task.Include.ShouldBeEmpty();
            }
            else
            {
                ITaskItem item = task.Include.ShouldHaveSingleItem();
                item.ItemSpec.ShouldBe(expectedItem.Replace('/', Path.DirectorySeparatorChar));
                item.GetMetadata("RecursiveDir").ShouldBe(expectedRecursiveDir.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        [Fact]
        public void WildcardWithTildes_UsesAbsolutePathsForAllFileAccess()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder projectDirectory = env.CreateFolder(createFolder: true);
            TransientTestFolder decoyDirectory = env.CreateFolder(createFolder: true);
            string nestedDirectory = Path.Combine(projectDirectory.Path, "sub~dir", "nested~dir");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(nestedDirectory, "file~name.txt"), "project");
            env.SetCurrentDirectory(decoyDirectory.Path);

            RecordingFileSystem fileSystem = new();
            env.WithTransientTestState(new FileMatcherTestState(new FileMatcher(fileSystem)));
            CreateItem task = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory.Path),
                Include = [new TaskItem(Path.Combine("sub~dir", "**", "*.txt"))],
            };

            task.Execute().ShouldBeTrue();

            ITaskItem item = task.Include.ShouldHaveSingleItem();
            item.ItemSpec.ShouldBe(Path.Combine("sub~dir", "nested~dir", "file~name.txt"));
            item.GetMetadata("RecursiveDir").ShouldBe("nested~dir" + Path.DirectorySeparatorChar);
            fileSystem.Paths.ShouldNotBeEmpty();
            fileSystem.Paths.ShouldAllBe(path => Path.IsPathRooted(path));
        }

        [Theory]
        [InlineData("project...dir")]
        [InlineData("project~dir")]
        public void Wildcard_ProjectDirectoryIsNotPartOfPattern(string directoryName)
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder root = env.CreateFolder(createFolder: true);
            TransientTestFolder projectDirectory = env.CreateFolder(Path.Combine(root.Path, directoryName));
            TransientTestFolder decoyDirectory = env.CreateFolder(createFolder: true);
            string nestedDirectory = Path.Combine(projectDirectory.Path, "sub", "nested");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(nestedDirectory, "child.txt"), "project");
            env.SetCurrentDirectory(decoyDirectory.Path);

            CreateItem task = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory.Path),
                Include = [new TaskItem(Path.Combine("sub", "**", "*.txt"))],
            };

            task.Execute().ShouldBeTrue();
            ITaskItem item = task.Include.ShouldHaveSingleItem();
            item.ItemSpec.ShouldBe(Path.Combine("sub", "nested", "child.txt"));
            item.GetMetadata("RecursiveDir").ShouldBe("nested" + Path.DirectorySeparatorChar);
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void PartiallyRootedWildcard_PreservesOutputRoot(bool driveRelative)
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder projectDirectory = env.CreateFolder(createFolder: true);
            TransientTestFolder decoyDirectory = env.CreateFolder(createFolder: true);
            string subdirectory = Path.Combine(projectDirectory.Path, "sub");
            Directory.CreateDirectory(subdirectory);
            File.WriteAllText(Path.Combine(subdirectory, "root.txt"), "project");
            env.SetCurrentDirectory(decoyDirectory.Path);

            string pattern = driveRelative
                ? projectDirectory.Path.Substring(0, 2) + @"sub\*.txt"
                : Path.Combine(projectDirectory.Path.Substring(2), "sub", "*.txt");
            string expectedItem = driveRelative
                ? projectDirectory.Path.Substring(0, 2) + @"sub\root.txt"
                : Path.Combine(projectDirectory.Path.Substring(2), "sub", "root.txt");
            CreateItem task = new()
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory.Path),
                Include = [new TaskItem(pattern)],
            };

            task.Execute().ShouldBeTrue();
            task.Include.ShouldHaveSingleItem().ItemSpec.ShouldBe(expectedItem);
        }

        [Fact]
        public void LiteralItems_UnaffectedByProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder directory = env.CreateFolder(createFolder: true);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory.Path),
                Include = [new TaskItem("A.txt"), new TaskItem("B.txt")],
                Exclude = [new TaskItem("B.txt")],
                AdditionalMetadata = ["Tag=Value"],
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            t.Include[0].ItemSpec.ShouldBe("A.txt");
            t.Include[0].GetMetadata("Tag").ShouldBe("Value");
        }

        private sealed class FileMatcherTestState : TransientTestState
        {
            private readonly FileMatcher _original;

            public FileMatcherTestState(FileMatcher matcher)
            {
                _original = FileMatcher.Default;
                FileMatcher.Default = matcher;
            }

            public override void Revert() => FileMatcher.Default = _original;
        }

        private sealed class RecordingFileSystem : ManagedFileSystem
        {
            public ConcurrentBag<string> Paths { get; } = [];

            public override bool DirectoryExists(string path)
            {
                Paths.Add(path);
                return base.DirectoryExists(path);
            }

            public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            {
                Paths.Add(path);
                return base.EnumerateFiles(path, searchPattern, searchOption);
            }

            public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
            {
                Paths.Add(path);
                return base.EnumerateDirectories(path, searchPattern, searchOption);
            }

            public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
            {
                Paths.Add(path);
                return base.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            }
        }
    }
}
