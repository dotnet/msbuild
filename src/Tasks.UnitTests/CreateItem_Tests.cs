// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class CreateItem_Tests
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
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/7330")]
        [WindowsOnlyTheory]
        [InlineData(@"z:\**")]
        [InlineData(@"z:\**\*.log")]
        [InlineData(@"z:\\\\**\*.log")]
        public void LogWindowsWarningUponCreateItemExecution(string itemSpec)
        {
            VerifyDriveEnumerationWarningLoggedUponCreateItemExecution(itemSpec);
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during task item creation on Unix platform.
        /// </summary>
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/7330")]
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
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/7330")]
        [WindowsOnlyTheory]
        [InlineData(
            CreateItemWithInclude,
            @"z:\**")]

        [InlineData(
            CreateItemWithInclude,
            @"z:\**\*.txt")]

        [InlineData(
            CreateItemWithInclude,
            @"z:$(empty)\**\*.cs")]
        public void LogWindowsWarningUponItemCreationWithDriveEnumeration(string content, string include)
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
        /// Logs warning when encountering wildcard drive enumeration during CreateItem task execution on Unix platform.
        /// </summary>
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/7330")]
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
    }
}
