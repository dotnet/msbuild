// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Definition;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    sealed public class CreateItem_Tests
    {
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
        /// Throw exception when encountering wildcard drive enumeration during task item creation.
        /// </summary>
        [Fact]
        public void WildcardDriveEnumerationTaskItemThrowsException()
        {
            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MsBuildCheckWildcardDriveEnumeration", "1");
                CreateItem t = new CreateItem();
                t.BuildEngine = new MockEngine();

                t.Include = new ITaskItem[] { new TaskItem(@"\**") };

                Assert.Throws<DriveEnumerationWildcardException>(() =>
                {
                    t.Execute();
                });
            }
        }

        /// <summary>
        /// Using the CreateItem task to expand wildcards and result in a failure due to
        /// attempted drive enumeration.
        /// </summary>
        [Fact]
        public void CreateItemEvaluationResultingInWildcardDriveEnumeration()
        {
            using var env = TestEnvironment.Create(_testOutput);
            env.SetEnvironmentVariable("MsBuildCheckWildcardDriveEnumeration", "1");

            string content =
                @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <Target Name =""TestTarget"" Returns=""@(Text)"">
                      <CreateItem Include=""\**\*.txt"">
                        <Output TaskParameter=""Include"" ItemName=""Text""/>
                      </CreateItem>
                    </Target>
                  </Project>
                ";

            var testFile = env.CreateFile(env.CreateFolder(), "a.csproj", content);
            var p = ProjectInstance.FromFile(testFile.Path, new ProjectOptions());

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildRequestData data = new BuildRequestData(p, new[] { "TestTarget" });
            BuildParameters parameters = new BuildParameters();
            BuildResult buildResult = buildManager.Build(parameters, data);
            buildResult.OverallResult.ShouldBe(BuildResultCode.Failure);
            buildResult["TestTarget"].Exception?.Message.ShouldContain("this resulted in an attempted drive enumeration");
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during task item creation.
        /// </summary>
        [Fact]
        public void CreateItemEvaluationResultingInLogWildcardDriveEnumeration()
        {
            using var env = TestEnvironment.Create(_testOutput);
            env.SetEnvironmentVariable("MsBuildCheckWildcardDriveEnumeration", "0");

            string content =
                @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <Target Name =""TestTarget"" Returns=""@(Text)"">
                      <CreateItem Include=""\**\*.txt"">
                        <Output TaskParameter=""Include"" ItemName=""Text""/>
                      </CreateItem>
                    </Target>
                  </Project>
                ";

            var testFile = env.CreateFile(env.CreateFolder(), "a.csproj", content);
            var p = ProjectInstance.FromFile(testFile.Path, new ProjectOptions());

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildRequestData data = new BuildRequestData(p, new[] { "TestTarget" });
            BuildParameters parameters = new BuildParameters();
            BuildResult buildResult = buildManager.Build(parameters, data); // Issue: Results in unauthorized access exception
            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);
        }

        /// <summary>
        /// Logs warning when encountering wildcard drive enumeration during task item creation.
        /// </summary>
        [Fact]
        public void WildcardDriveEnumerationTaskItemLogsWarning()
        {
            using (var env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MsBuildCheckWildcardDriveEnumeration", "0");
                CreateItem t = new CreateItem();
                MockEngine engine = new MockEngine();

                t.BuildEngine = engine;
                t.Include = new ITaskItem[] { new TaskItem(@"\**") }; // Issue: Results in unauthorized access exception

                bool succeeded = t.Execute();

                Assert.True(succeeded);
                Assert.Equal(1, engine.Warnings);
            }
        }
    }
}



