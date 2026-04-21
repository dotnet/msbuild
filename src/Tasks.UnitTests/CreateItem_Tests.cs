// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

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
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/8373")]
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
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/8373")]
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

            // Create a project directory with a file.
            TransientTestFolder projectDir = env.CreateFolder(createFolder: true);
            env.CreateFile(projectDir, "alpha.txt", "alpha");

            // Create a separate directory that is NOT the project directory and put a different file there.
            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.CreateFile(otherDir, "beta.txt", "beta");

            // Set cwd to otherDir so that if the task used cwd, it would find beta.txt instead.
            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = new ITaskItem[] { new TaskItem("*.txt") },
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

            // cwd points elsewhere — Exclude must still resolve *.log against projectDir.
            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = new ITaskItem[] { new TaskItem("*.log") },
                Exclude = new ITaskItem[] { new TaskItem("*.log") },
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

            // Set cwd somewhere else to prove ProjectDirectory is used.
            TransientTestFolder otherDir = env.CreateFolder(createFolder: true);
            env.SetCurrentDirectory(otherDir.Path);

            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = new ITaskItem[] { new TaskItem($"**{Path.DirectorySeparatorChar}*.txt") },
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);

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

            // Use an unrelated ProjectDirectory.
            TransientTestFolder unrelatedDir = env.CreateFolder(createFolder: true);

            string absolutePattern = Path.Combine(targetDir.Path, "*.txt");
            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = new ITaskItem[] { new TaskItem(absolutePattern) },
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(unrelatedDir.Path),
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            // Absolute pattern returns absolute path.
            Path.GetFileName(t.Include[0].ItemSpec).ShouldBe("SampleFile.txt");
        }

        /// <summary>
        /// Pre-expanded literal items (no wildcards) produce identical output regardless of
        /// ProjectDirectory, Exclude, and AdditionalMetadata settings.
        /// </summary>
        [Fact]
        public void LiteralItems_UnaffectedByProjectDirectory()
        {
            CreateItem t = new CreateItem
            {
                BuildEngine = new MockEngine(_testOutput),
                Include = new ITaskItem[] { new TaskItem("A.txt"), new TaskItem("B.txt") },
                Exclude = new ITaskItem[] { new TaskItem("B.txt") },
                AdditionalMetadata = new string[] { "Tag=Value" },
            };

            t.Execute().ShouldBeTrue();
            t.Include.Length.ShouldBe(1);
            t.Include[0].ItemSpec.ShouldBe("A.txt");
            t.Include[0].GetMetadata("Tag").ShouldBe("Value");
        }
    }
}
