// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if FEATURE_COMPILE_IN_TESTS
using System.Reflection;
#endif

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#if FEATURE_COMPILE_IN_TESTS
using EscapingUtilities = Microsoft.Build.Shared.EscapingUtilities;
#endif
using FileUtilities = Microsoft.Build.Shared.FileUtilities;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ResourceUtilities = Microsoft.Build.Shared.ResourceUtilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
#if FEATURE_COMPILE_IN_TESTS
using Microsoft.Build.Shared;
#endif

#nullable disable

namespace Microsoft.Build.UnitTests.EscapingInProjects_Tests
{
    /// <summary>
    /// Test task that just logs the parameters it receives.
    /// </summary>
    public class MyTestTask : Task
    {
        private ITaskItem _taskItemParam;
        public ITaskItem TaskItemParam
        {
            get
            {
                return _taskItemParam;
            }

            set
            {
                _taskItemParam = value;
            }
        }

        public override bool Execute()
        {
            if (TaskItemParam != null)
            {
                Log.LogMessageFromText("Received TaskItemParam: " + TaskItemParam.ItemSpec, MessageImportance.High);
            }

            return true;
        }
    }

    public class SimpleScenarios : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public SimpleScenarios(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        /// <summary>
        /// Since we create a project with the same name in many of these tests, and two projects with
        /// the same name cannot be loaded in a ProjectCollection at the same time, we should unload the
        /// GlobalProjectCollection (into which all of these projects are placed by default) after each test.
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// a string parameter of a task, in this case the Message task.
        /// </summary>
        [Fact]
        public void SemicolonInPropertyPassedIntoStringParam()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>
                    <Target Name=`Build`>
                        <Message Text=`Property value is '$(MyPropertyWithSemicolons)'` />
                    </Target>
                </Project>
                ", logger: new MockLogger(_output));

            logger.AssertLogContains("Property value is 'abc ; def ; ghi'");
        }

        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// a string parameter of a task, in this case the Message task.
        /// </summary>
        [Fact]
        public void SemicolonInPropertyPassedIntoStringParam_UsingTaskHost()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <UsingTask TaskName=`Message` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` TaskFactory=`TaskHostFactory` />
                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>
                    <Target Name=`Build`>
                        <Message Text=`Property value is '$(MyPropertyWithSemicolons)'` />
                    </Target>
                </Project>
                ", logger: new MockLogger(_output));

            logger.AssertLogContains("Property value is 'abc ; def ; ghi'");
        }

#if FEATURE_ASSEMBLY_LOCATION
        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// an ITaskItem[] task parameter.
        /// </summary>
        [Fact]
        public void SemicolonInPropertyPassedIntoITaskItemParam()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@$"

                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <UsingTask TaskName=`Microsoft.Build.UnitTests.EscapingInProjects_Tests.MyTestTask` AssemblyFile=`{new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath}` />

                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>

                    <Target Name=`Build`>
                        <MyTestTask TaskItemParam=`123 $(MyPropertyWithSemicolons) 789` />
                    </Target>

                </Project>

                ",
                logger: new MockLogger(_output));

            logger.AssertLogContains("Received TaskItemParam: 123 abc ; def ; ghi 789");
        }

        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// an ITaskItem[] task parameter.
        /// </summary>
        [Fact]
        public void SemicolonInPropertyPassedIntoITaskItemParam_UsingTaskHost()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(String.Format(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <UsingTask TaskName=`Microsoft.Build.UnitTests.EscapingInProjects_Tests.MyTestTask` AssemblyFile=`{0}` TaskFactory=`TaskHostFactory` />

                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>

                    <Target Name=`Build`>
                        <MyTestTask TaskItemParam=`123 $(MyPropertyWithSemicolons) 789` />
                    </Target>

                </Project>

                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath),
                logger: new MockLogger(_output));

            logger.AssertLogContains("Received TaskItemParam: 123 abc ; def ; ghi 789");
        }
#endif

        /// <summary>
        /// If I try to add a new item to a project, and my new item's Include has an unescaped semicolon
        /// in it, then we shouldn't try to match it up against any existing wildcards.  This is a really
        /// bizarre scenario ... the caller probably meant to escape the semicolon.
        /// </summary>
        [Fact]
        public void AddNewItemWithSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                        <MyWildCard Include=`foo;bar.weirdo`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);
            project.AddItem("MyWildCard", "foo;bar.weirdo");

            Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);
        }

        /// <summary>
        /// If I try to add a new item to a project, and my new item's Include has a property that
        /// contains an unescaped semicolon in it, then we shouldn't try to match it up against any existing
        /// wildcards.
        /// </summary>
        [Fact]
        public void AddNewItemWithPropertyContainingSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>
                    <ItemGroup>
                        <MyWildCard Include=`$(FilenameWithSemicolon).weirdo`/>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);
            project.AddItem("MyWildCard", "$(FilenameWithSemicolon).weirdo");

            Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has an unescaped semicolon
        /// in it, then we shouldn't try to match it up against any existing wildcards.  This is a really
        /// bizarre scenario ... the caller probably meant to escape the semicolon.
        /// </summary>
        [Fact]
        public void ModifyItemIncludeSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>

                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <MyWildcard Include=`a.weirdo` />
                        <MyWildcard Include=`foo;bar.weirdo` />
                        <MyWildcard Include=`c.weirdo` />
                    </ItemGroup>

                </Project>
                ";

            try
            {
                // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
                EscapingInProjectsHelper.CreateThreeWeirdoFiles();

                Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

                EscapingInProjectsHelper.ModifyItemOfTypeInProject(project, "MyWildcard", "b.weirdo", "foo;bar.weirdo");

                Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has an escaped semicolon
        /// in it, and it matches the existing wildcard, then we shouldn't need to modify the project file.
        /// </summary>
        [Fact]
        public void ModifyItemIncludeEscapedSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>

                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>

                </Project>
                ";

            try
            {
                // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
                EscapingInProjectsHelper.CreateThreeWeirdoFiles();

                Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

                IEnumerable<ProjectItem> newItems = EscapingInProjectsHelper.ModifyItemOfTypeInProject(project, "MyWildcard", "b.weirdo", "foo%253Bbar.weirdo");

                Assert.Single(newItems);
                Assert.Equal("*.weirdo", newItems.First().UnevaluatedInclude);
                Assert.Equal("foo%3Bbar.weirdo", newItems.First().EvaluatedInclude);
                Assert.Equal("foo%253Bbar.weirdo", Project.GetEvaluatedItemIncludeEscaped(newItems.First()));

                Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has a property that
        /// contains an unescaped semicolon in it, then we shouldn't try to match it up against any existing
        /// wildcards.
        /// </summary>
        [Fact]
        public void ModifyItemAddPropertyContainingSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>

                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>

                    <ItemGroup>
                        <MyWildcard Include=`a.weirdo` />
                        <MyWildcard Include=`$(FilenameWithSemicolon).weirdo` />
                        <MyWildcard Include=`c.weirdo` />
                    </ItemGroup>

                </Project>
                ";

            try
            {
                // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
                EscapingInProjectsHelper.CreateThreeWeirdoFiles();

                Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

                EscapingInProjectsHelper.ModifyItemOfTypeInProject(project, "MyWildcard", "b.weirdo", "$(FilenameWithSemicolon).weirdo");

                Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Make sure that character escaping works as expected when adding a new item that matches
        /// an existing wildcarded item in the project file.
        /// </summary>
        [Fact]
        public void AddNewItemThatMatchesWildcard1()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);
            IEnumerable<ProjectItem> newItems = project.AddItem("MyWildCard", "foo%253bbar.weirdo");

            Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);

            Assert.Single(newItems);
            Assert.Equal("MyWildCard", newItems.First().ItemType); // "Newly added item should have correct ItemType"
            Assert.Equal("*.weirdo", newItems.First().UnevaluatedInclude); // "Newly added item should have correct UnevaluatedInclude"
            Assert.Equal("foo%253bbar.weirdo", Project.GetEvaluatedItemIncludeEscaped(newItems.First())); // "Newly added item should have correct EvaluatedIncludeEscaped"
            Assert.Equal("foo%3bbar.weirdo", newItems.First().EvaluatedInclude); // "Newly added item should have correct EvaluatedInclude"
        }

        /// <summary>
        /// Make sure that character escaping works as expected when adding a new item that matches
        /// an existing wildcarded item in the project file.
        /// </summary>
        [Fact]
        public void AddNewItemThatMatchesWildcard2()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.AAA%253bBBB`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <MyWildCard Include=`*.AAA%253bBBB`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);
            IEnumerable<ProjectItem> newItems = project.AddItem("MyWildCard", "foo.AAA%253bBBB");

            Helpers.CompareProjectXml(projectNewExpectedContents, project.Xml.RawXml);

            Assert.Single(newItems);
            Assert.Equal("MyWildCard", newItems.First().ItemType); // "Newly added item should have correct ItemType"
            Assert.Equal("*.AAA%253bBBB", newItems.First().UnevaluatedInclude); // "Newly added item should have correct UnevaluatedInclude"
            Assert.Equal("foo.AAA%253bBBB", Project.GetEvaluatedItemIncludeEscaped(newItems.First())); // "Newly added item should have correct EvaluatedIncludeEscaped"
            Assert.Equal("foo.AAA%3bBBB", newItems.First().EvaluatedInclude); // "Newly added item should have correct EvaluatedInclude"
        }

        /// <summary>
        /// Make sure that all inferred task outputs (those that are determined without actually
        /// executing the task) are left escaped when they become real items in the engine, and
        /// they only get unescaped when fed into a subsequent task.
        /// </summary>
        [Fact]
        public void InferEscapedOutputsFromTask()
        {
            string inputFile = null;
            string outputFile = null;

            try
            {
                inputFile = FileUtilities.GetTemporaryFile();
                outputFile = FileUtilities.GetTemporaryFile();

                MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(String.Format(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>

                    <Target Name=`GenerateResources` Inputs=`{0}` Outputs=`{1}`>
                        <NonExistentTask OutputResources=`aaa%253bbbb.resx; ccc%253bddd.resx`>
                            <Output ItemName=`Resource` TaskParameter=`OutputResources`/>
                        </NonExistentTask>
                    </Target>

                    <Target Name=`Build` DependsOnTargets=`GenerateResources`>
                        <Message Text=`Resources = @(Resource)`/>
                    </Target>

                </Project>

                ", inputFile, outputFile),
                logger: new MockLogger(_output));

                logger.AssertLogContains("Resources = aaa%3bbbb.resx;ccc%3bddd.resx");
            }
            finally
            {
                if (inputFile != null)
                {
                    File.Delete(inputFile);
                }

                if (outputFile != null)
                {
                    File.Delete(outputFile);
                }
            }
        }

        /// <summary>
        /// Do an item transform, where the transform expression contains an unescaped semicolon as well
        /// as an escaped percent sign.
        /// </summary>
        [Fact]
        public void ItemTransformContainingSemicolon()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <ItemGroup>
                        <TextFile Include=`X.txt`/>
                        <TextFile Include=`Y.txt`/>
                        <TextFile Include=`Z.txt`/>
                    </ItemGroup>
                    <Target Name=`Build`>
                        <Message Text=`Transformed item list: '@(TextFile->'%(FileName);%(FileName)%253b%(FileName)%(Extension)','    ')'` />
                    </Target>
                </Project>

                ", logger: new MockLogger(_output));

            logger.AssertLogContains("Transformed item list: 'X;X%3bX.txt    Y;Y%3bY.txt    Z;Z%3bZ.txt'");
        }

        /// <summary>
        /// Do an item transform, where the transform expression contains an unescaped semicolon as well
        /// as an escaped percent sign.
        /// </summary>
        [Fact]
        public void ItemTransformContainingSemicolon_InTaskHost()
        {
            MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <UsingTask TaskName=`Message` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` TaskFactory=`TaskHostFactory` />

                    <ItemGroup>
                        <TextFile Include=`X.txt`/>
                        <TextFile Include=`Y.txt`/>
                        <TextFile Include=`Z.txt`/>
                    </ItemGroup>
                    <Target Name=`Build`>
                        <Message Text=`Transformed item list: '@(TextFile->'%(FileName);%(FileName)%253b%(FileName)%(Extension)','    ')'` />
                    </Target>
                </Project>

                ", logger: new MockLogger(_output));

            logger.AssertLogContains("Transformed item list: 'X;X%3bX.txt    Y;Y%3bY.txt    Z;Z%3bZ.txt'");
        }

        /// <summary>
        /// Tests that when we add an item and are in a directory with characters in need of escaping, and the
        /// item's FullPath metadata is retrieved, that a properly un-escaped version of the path is returned
        /// </summary>
        [Fact]
        public void FullPathMetadataOnItemUnescaped()
        {
            string projectName = "foo.proj";
            string projectRelativePath = "(jay's parens test)";
            string path = Path.Combine(Path.GetTempPath(), projectRelativePath);
            string projectAbsolutePath = Path.Combine(path, projectName);

            try
            {
                Directory.CreateDirectory(path);

                ProjectRootElement projectElement = ProjectRootElement.Create(projectAbsolutePath);
                ProjectItemGroupElement itemgroup = projectElement.AddItemGroup();
                itemgroup.AddItem("ProjectFile", projectName);

                Project project = new Project(projectElement, null, null, new ProjectCollection());
                ProjectInstance projectInstance = project.CreateProjectInstance();

                IEnumerable<ProjectItemInstance> items = projectInstance.GetItems("ProjectFile");
                Assert.Equal(projectAbsolutePath, items.First().GetMetadataValue("FullPath"));
            }
            finally
            {
                if (File.Exists(projectAbsolutePath))
                {
                    File.Delete(projectAbsolutePath);
                }

                if (Directory.Exists(path))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(path);
                }
            }
        }


        /// <summary>
        /// Test that we can pass in global properties containing escaped characters and they
        /// won't be unescaped.
        /// </summary>
        [Fact]
        public void GlobalPropertyWithEscapedCharacters()
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`Build`>
                        <Message Text=`MyGlobalProperty = '$(MyGlobalProperty)'` />
                    </Target>
                </Project>
                ");

            project.SetGlobalProperty("MyGlobalProperty", "foo%253bbar");

            bool success = project.Build(logger);
            Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"

            logger.AssertLogContains("MyGlobalProperty = 'foo%3bbar'");
        }

        /// <summary>
        /// If %2A (escaped '*') or %3F (escaped '?') is in an item's Include, it should be treated
        /// literally, not as a wildcard
        /// </summary>
        [Fact]
        public void EscapedWildcardsShouldNotBeExpanded()
        {
            MockLogger logger = new MockLogger();

            try
            {
                // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
                EscapingInProjectsHelper.CreateThreeWeirdoFiles();
                Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`t`>
                        <ItemGroup>
                            <type Include=`%2A` Exclude=``/>
                        </ItemGroup>
                        <Message Text=`[@(type)]`/>
                    </Target>
                </Project>
                ");

                bool success = project.Build(logger);
                Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"
                logger.AssertLogContains("[*]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// If %2A (escaped '*') or %3F (escaped '?') is in an item's Include, it should be treated
        /// literally, not as a wildcard
        /// </summary>
        [Fact]
        public void EscapedWildcardsShouldNotBeExpanded_InTaskHost()
        {
            MockLogger logger = new();

            try
            {
                // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
                EscapingInProjectsHelper.CreateThreeWeirdoFiles();
                Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project>
                    <UsingTask TaskName=`Message` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` TaskFactory=`TaskHostFactory` />

                    <Target Name=`t`>
                        <ItemGroup>
                            <type Include=`%2A` Exclude=``/>
                        </ItemGroup>
                        <Message Text=`[@(type)]`/>
                    </Target>
                </Project>
                ");

                project.Build(logger).ShouldBeTrue("Build failed.  See test output (Attachments in Azure Pipelines) for details");
                logger.AssertLogContains("[*]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Parity with Orcas: Target names are always unescaped, and in fact, if there are two targets,
        /// one the escaped version of the other, the second will override the first as though they had the
        /// same name.
        /// </summary>
        [Fact]
        public void TargetNamesAlwaysUnescaped()
        {
            bool exceptionCaught = false;

            try
            {
                ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`%24` />
                </Project>
                ");
            }
            catch (InvalidProjectFileException ex)
            {
                string expectedErrorMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("NameInvalid", "$", "$");
                Assert.Equal(expectedErrorMessage, ex.Message); // "Wrong error message"
                exceptionCaught = true;
            }

            Assert.True(exceptionCaught); // "Expected an InvalidProjectFileException"
        }

        /// <summary>
        /// Parity with Orcas: Target names are always unescaped, and in fact, if there are two targets,
        /// one the escaped version of the other, the second will override the first as though they had the
        /// same name.
        /// </summary>
        [Fact]
        public void TargetNamesAlwaysUnescaped_Override()
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <Target Name=`%3B`>
                    <Message Text=`[WRONG]` />
                </Target>
                <Target Name=`;`>
                    <Message Text=`[OVERRIDE]` />
                </Target>
            </Project>
            ");
            MockLogger logger = new MockLogger();

            bool success = project.Build(logger);
            Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"
            logger.AssertLogContains("[OVERRIDE]");
        }

        /// <summary>
        /// Tests that when we set metadata through the evaluation model, we do the right thing
        /// </summary>
        [Fact]
        public void SpecialCharactersInMetadataValueConstruction()
        {
            string projectString = @"
                <Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"">
                    <ItemGroup>
                        <None Include='MetadataTests'>
                            <EscapedSemicolon>%3B</EscapedSemicolon>
                            <EscapedDollarSign>%24</EscapedDollarSign>
                        </None>
                    </ItemGroup>
                </Project>";
            System.Xml.XmlReader reader = new System.Xml.XmlTextReader(new StringReader(projectString));
            Project project = new Project(reader);
            ProjectItem item = project.GetItems("None").Single();

            EscapingInProjectsHelper.SpecialCharactersInMetadataValueTests(item);
        }

        /// <summary>
        /// Tests that when we set metadata through the evaluation model, we do the right thing
        /// </summary>
        [Fact]
        public void SpecialCharactersInMetadataValueEvaluation()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("None", "MetadataTests", new Dictionary<string, string> {
                {"EscapedSemicolon", "%3B"}, // Microsoft.Build.Evaluation.ProjectCollection.Escape(";")
                {"EscapedDollarSign", "%24"}, // Microsoft.Build.Evaluation.ProjectCollection.Escape("$")
            }).Single();

            EscapingInProjectsHelper.SpecialCharactersInMetadataValueTests(item);
            project.ReevaluateIfNecessary();
            EscapingInProjectsHelper.SpecialCharactersInMetadataValueTests(item);
        }

        /// <summary>
        /// Say you have a scenario where a user is allowed to specify an arbitrary set of files (or
        /// any sort of items) and expects to be able to get them back out as they were sent in.  In addition,
        /// the user can specify a macro (property) that can resolve to yet another arbitrary set of items.
        /// We want to make sure that we do the right thing (assuming that the user escaped the information
        /// correctly coming in) and don't mess up their set of items
        /// </summary>
        [Fact]
        public void CanGetCorrectListOfItemsWithSemicolonsInThem()
        {
            string projectString = @"
                <Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"">
                    <PropertyGroup>
                        <MyUserMacro>foo%3bbar</MyUserMacro>
                    </PropertyGroup>
                    <ItemGroup>
                        <DifferentList Include=""a"" />
                        <DifferentList Include=""b%3bc"" />
                        <DifferentList Include=""$(MyUserMacro)"" />
                    </ItemGroup>
                </Project>";

            System.Xml.XmlReader reader = new System.Xml.XmlTextReader(new StringReader(projectString));
            Project project = new Project(reader);
            IEnumerable<ProjectItem> items = project.GetItems("DifferentList");

            Assert.Equal(3, items.Count());
            Assert.Equal("a", items.ElementAt(0).EvaluatedInclude);
            Assert.Equal("b;c", items.ElementAt(1).EvaluatedInclude);
            Assert.Equal("foo;bar", items.ElementAt(2).EvaluatedInclude);
        }

        /// <summary>
        /// Say you have a scenario where a user is allowed to specify an arbitrary set of files (or
        /// any sort of items) and expects to be able to get them back out as they were sent in.  In addition,
        /// the user can specify a macro (property) that can resolve to yet another arbitrary set of items.
        /// We want to make sure that we do the right thing (assuming that the user escaped the information
        /// correctly coming in) and don't mess up their set of items
        /// </summary>
        [Fact]
        public void CanGetCorrectListOfItemsWithSemicolonsInThem2()
        {
            string projectString = @"
                <Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"">
                    <PropertyGroup>
                        <MyUserMacro>foo;bar</MyUserMacro>
                    </PropertyGroup>
                    <ItemGroup>
                        <DifferentList Include=""a"" />
                        <DifferentList Include=""b%3bc"" />
                        <DifferentList Include=""$(MyUserMacro)"" />
                    </ItemGroup>
                </Project>";

            System.Xml.XmlReader reader = new System.Xml.XmlTextReader(new StringReader(projectString));
            Project project = new Project(reader);
            IEnumerable<ProjectItem> items = project.GetItems("DifferentList");

            Assert.Equal(4, items.Count());
            Assert.Equal("a", items.ElementAt(0).EvaluatedInclude);
            Assert.Equal("b;c", items.ElementAt(1).EvaluatedInclude);
            Assert.Equal("foo", items.ElementAt(2).EvaluatedInclude);
            Assert.Equal("bar", items.ElementAt(3).EvaluatedInclude);
        }
    }

#if FEATURE_COMPILE_IN_TESTS
    public class FullProjectsUsingMicrosoftCommonTargets
    {
        private readonly ITestOutputHelper _testOutput;

        public FullProjectsUsingMicrosoftCommonTargets(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        private const string SolutionFileContentsWithUnusualCharacters = @"Microsoft Visual Studio Solution File, Format Version 11.00
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `Cons.ole;!@(foo)'^(Application1`, `Console;!@(foo)'^(Application1\Cons.ole;!@(foo)'^(Application1.csproj`, `{770F2381-8C39-49E9-8C96-0538FA4349A7}`
                EndProject
                Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `Class;!@(foo)'^(Library1`, `Class;!@(foo)'^(Library1\Class;!@(foo)'^(Library1.csproj`, `{0B4B78CC-C752-43C2-BE9A-319D20216129}`
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {770F2381-8C39-49E9-8C96-0538FA4349A7}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {770F2381-8C39-49E9-8C96-0538FA4349A7}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {770F2381-8C39-49E9-8C96-0538FA4349A7}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {770F2381-8C39-49E9-8C96-0538FA4349A7}.Release|Any CPU.Build.0 = Release|Any CPU
                        {0B4B78CC-C752-43C2-BE9A-319D20216129}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {0B4B78CC-C752-43C2-BE9A-319D20216129}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {0B4B78CC-C752-43C2-BE9A-319D20216129}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {0B4B78CC-C752-43C2-BE9A-319D20216129}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

        /// <summary>
        ///     ESCAPING: Escaping in conditionals is broken.
        /// </summary>
        [Fact]
        public void SemicolonInConfiguration()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>ClassLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'a%3bb%27c|AnyCPU' `>
                        <OutputPath>bin\a%3bb%27c\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

            // ---------------------
            // Class1.cs
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

            // Create a logger.
            MockLogger logger = new MockLogger();

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("foo.csproj");

            // Build the default targets using the Configuration "a;b'c".
            project.SetGlobalProperty("Configuration", EscapingUtilities.Escape("a;b'c"));
            bool success = project.Build(logger);
            Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\a;b'c\ClassLibrary16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\a;b'c\ClassLibrary16.dll");

            logger.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\a;b'c\ClassLibrary16.dll")));
        }

        /// <summary>
        ///     ESCAPING: Escaping in conditionals is broken.
        /// </summary>
        [Fact]
        public void SemicolonInConfiguration_UsingTaskHost()
        {
            string originalOverrideTaskHostVariable = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ObjectModelHelpers.DeleteTempProjectDirectory();

                // ---------------------
                // Foo.csproj
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>ClassLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'a%3bb%27c|AnyCPU' `>
                        <OutputPath>bin\a%3bb%27c\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

                // ---------------------
                // Class1.cs
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

                // Create a logger.
                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("foo.csproj");

                // Build the default targets using the Configuration "a;b'c".
                project.SetGlobalProperty("Configuration", EscapingUtilities.Escape("a;b'c"));
                bool success = project.Build(logger);
                Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"

                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\a;b'c\ClassLibrary16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\a;b'c\ClassLibrary16.dll");

                logger.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\a;b'c\ClassLibrary16.dll")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalOverrideTaskHostVariable);
            }
        }

        /// <summary>
        ///     ESCAPING: CopyBuildTarget target fails if the output assembly name contains a semicolon or single-quote
        /// </summary>
        [Fact]
        public void SemicolonInAssemblyName()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>Class%3bLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

            // ---------------------
            // Class1.cs
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

            MockLogger log = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.dll", @"Did not find expected file obj\debug\Class;Library16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.pdb", @"Did not find expected file obj\debug\Class;Library16.pdb");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class;Library16.dll", @"Did not find expected file bin\debug\Class;Library16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.pdb", @"Did not find expected file obj\debug\Class;Library16.pdb");

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class;Library16.dll")));
        }

        /// <summary>
        ///     ESCAPING: CopyBuildTarget target fails if the output assembly name contains a semicolon or single-quote
        /// </summary>
        [Fact]
        public void SemicolonInAssemblyName_UsingTaskHost()
        {
            string originalOverrideTaskHostVariable = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ObjectModelHelpers.DeleteTempProjectDirectory();

                // ---------------------
                // Foo.csproj
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>Class%3bLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

                // ---------------------
                // Class1.cs
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

                MockLogger log = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.dll", @"Did not find expected file obj\debug\Class;Library16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.pdb", @"Did not find expected file obj\debug\Class;Library16.pdb");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class;Library16.dll", @"Did not find expected file bin\debug\Class;Library16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class;Library16.pdb", @"Did not find expected file obj\debug\Class;Library16.pdb");

                log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class;Library16.dll")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalOverrideTaskHostVariable);
            }
        }

        /// <summary>
        ///     ESCAPING: Conversion Issue: Properties with $(xxx) as literals are not being converted correctly
        /// </summary>
        [Fact]
        public void DollarSignInAssemblyName()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>Class%24%28prop%29Library16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

            // ---------------------
            // Class1.cs
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

            MockLogger log = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class$(prop)Library16.dll", @"Did not find expected file obj\debug\Class$(prop)Library16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class$(prop)Library16.pdb", @"Did not find expected file obj\debug\Class$(prop)Library16.pdb");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class$(prop)Library16.dll", @"Did not find expected file bin\debug\Class$(prop)Library16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class$(prop)Library16.pdb", @"Did not find expected file bin\debug\Class$(prop)Library16.pdb");

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class$(prop)Library16.dll")));
        }

        /// <summary>
        ///     ESCAPING: Conversion Issue: Properties with $(xxx) as literals are not being converted correctly
        /// </summary>
        [Fact]
        public void DollarSignInAssemblyName_UsingTaskHost()
        {
            string originalOverrideTaskHostVariable = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ObjectModelHelpers.DeleteTempProjectDirectory();

                // ---------------------
                // Foo.csproj
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>Class%24%28prop%29Library16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

                // ---------------------
                // Class1.cs
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

                MockLogger log = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class$(prop)Library16.dll", @"Did not find expected file obj\debug\Class$(prop)Library16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\Class$(prop)Library16.pdb", @"Did not find expected file obj\debug\Class$(prop)Library16.pdb");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class$(prop)Library16.dll", @"Did not find expected file bin\debug\Class$(prop)Library16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\Class$(prop)Library16.pdb", @"Did not find expected file bin\debug\Class$(prop)Library16.pdb");

                log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class$(prop)Library16.dll")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalOverrideTaskHostVariable);
            }
        }

        /// <summary>
        /// This is the case when one of the source code files in the project has a filename containing a semicolon.
        /// </summary>
        [Fact]
        public void SemicolonInSourceCodeFilename()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>ClassLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class%3b1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

            // ---------------------
            // Class1.cs
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class;1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

            MockLogger log = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\ClassLibrary16.dll", @"Did not find expected file obj\debug\ClassLibrary16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\ClassLibrary16.pdb", @"Did not find expected file obj\debug\ClassLibrary16.pdb");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\ClassLibrary16.dll", @"Did not find expected file bin\debug\ClassLibrary16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\ClassLibrary16.pdb", @"Did not find expected file bin\debug\ClassLibrary16.pdb");

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\ClassLibrary16.dll")));
        }

        /// <summary>
        /// This is the case when one of the source code files in the project has a filename containing a semicolon.
        /// </summary>
        [Fact]
        public void SemicolonInSourceCodeFilename_UsingTaskHost()
        {
            string originalOverrideTaskHostVariable = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ObjectModelHelpers.DeleteTempProjectDirectory();

                // ---------------------
                // Foo.csproj
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", $@"
                <Project DefaultTargets=`Build`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                        <OutputType>Library</OutputType>
                        <AssemblyName>ClassLibrary16</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <OutputPath>bin\Debug\</OutputPath>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Compile Include=`Class%3b1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            ");

                // ---------------------
                // Class1.cs
                // ---------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory("Class;1.cs", @"
                namespace ClassLibrary16
                {
                    public class Class1
                    {
                    }
                }
            ");

                MockLogger log = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj", log);

                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\ClassLibrary16.dll", @"Did not find expected file obj\debug\ClassLibrary16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\debug\ClassLibrary16.pdb", @"Did not find expected file obj\debug\ClassLibrary16.pdb");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\ClassLibrary16.dll", @"Did not find expected file bin\debug\ClassLibrary16.dll");
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\debug\ClassLibrary16.pdb", @"Did not find expected file bin\debug\ClassLibrary16.pdb");

                log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\ClassLibrary16.dll")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalOverrideTaskHostVariable);
            }
        }

        /// <summary>
        /// Build a .SLN file using MSBuild.  The .SLN and the projects contained within
        /// have all sorts of different characters in their name. There
        /// is even a P2P reference between the two projects in the .SLN.
        /// </summary>
        [Fact(Skip = "This is a known issue in Roslyn. This test should be enabled if Roslyn is updated for this scenario.")]
        public void SolutionWithLotsaDifferentCharacters()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------------------------------------------------------
            // Console;!@(foo)'^(Application1.sln
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln",
                SolutionFileContentsWithUnusualCharacters);

            // ---------------------------------------------------------------------
            // Console;!@(foo)'^(Application1.csproj
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\Cons.ole;!@(foo)'^(Application1.csproj",

                @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{770F2381-8C39-49E9-8C96-0538FA4349A7}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Console____foo____Application1</RootNamespace>
                        <AssemblyName>Console%3b!%40%28foo%29%27^%28Application1</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Program.cs` />
                    </ItemGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\Class%3b!%40%28foo%29%27^%28Library1\Class%3b!%40%28foo%29%27^%28Library1.csproj`>
                            <Project>{0B4B78CC-C752-43C2-BE9A-319D20216129}</Project>
                            <Name>Class%3b!%40%28foo%29%27^%28Library1</Name>
                        </ProjectReference>
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            // ---------------------------------------------------------------------
            // Program.cs
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\Program.cs",

                @"
                using System;
                using System.Collections.Generic;
                using System.Text;

                namespace Console____foo____Application1
                {
                    class Program
                    {
                        static void Main(string[] args)
                        {
                            Class____foo____Library1.Class1 foo = new Class____foo____Library1.Class1();
                        }
                    }
                }
                ");

            // ---------------------------------------------------------------------
            // Class;!@(foo)'^(Library1.csproj
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Class;!@(foo)'^(Library1\Class;!@(foo)'^(Library1.csproj",

                @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{0B4B78CC-C752-43C2-BE9A-319D20216129}</ProjectGuid>
                        <OutputType>Library</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Class____foo____Library1</RootNamespace>
                        <AssemblyName>Class%3b!%40%28foo%29%27^%28Library1</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            // ---------------------------------------------------------------------
            // Class1.cs
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Class;!@(foo)'^(Library1\Class1.cs",

                @"
                namespace Class____foo____Library1
                {
                    public class Class1
                    {
                    }
                }
                ");

            // Cons.ole;!@(foo)'^(Application1
            string targetForFirstProject = "Cons_ole_!__foo__^_Application1";

            MockLogger log = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln", new string[] { targetForFirstProject }, null, log);

            Assert.True(File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\bin\debug\Console;!@(foo)'^(Application1.exe"))); // @"Did not find expected file Console;!@(foo)'^(Application1.exe"
        }

        /// <summary>
        /// Build a .SLN file using MSBuild.  The .SLN and the projects contained within
        /// have all sorts of different characters in their name. There
        /// is even a P2P reference between the two projects in the .SLN.
        /// </summary>
        [Fact(Skip = "This is a known issue in Roslyn. This test should be enabled if Roslyn is updated for this scenario.")]
        public void SolutionWithLotsaDifferentCharacters_UsingTaskHost()
        {
            string originalOverrideTaskHostVariable = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ObjectModelHelpers.DeleteTempProjectDirectory();

                // ---------------------------------------------------------------------
                // Console;!@(foo)'^(Application1.sln
                // ---------------------------------------------------------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory(
                    @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln",
                    SolutionFileContentsWithUnusualCharacters);

                // ---------------------------------------------------------------------
                // Console;!@(foo)'^(Application1.csproj
                // ---------------------------------------------------------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory(
                    @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\Cons.ole;!@(foo)'^(Application1.csproj",

                    @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{770F2381-8C39-49E9-8C96-0538FA4349A7}</ProjectGuid>
                        <OutputType>Exe</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Console____foo____Application1</RootNamespace>
                        <AssemblyName>Console%3b!%40%28foo%29%27^%28Application1</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Program.cs` />
                    </ItemGroup>
                    <ItemGroup>
                        <ProjectReference Include=`..\Class%3b!%40%28foo%29%27^%28Library1\Class%3b!%40%28foo%29%27^%28Library1.csproj`>
                            <Project>{0B4B78CC-C752-43C2-BE9A-319D20216129}</Project>
                            <Name>Class%3b!%40%28foo%29%27^%28Library1</Name>
                        </ProjectReference>
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

                // ---------------------------------------------------------------------
                // Program.cs
                // ---------------------------------------------------------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory(
                    @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\Program.cs",

                    @"
                using System;
                using System.Collections.Generic;
                using System.Text;

                namespace Console____foo____Application1
                {
                    class Program
                    {
                        static void Main(string[] args)
                        {
                            Class____foo____Library1.Class1 foo = new Class____foo____Library1.Class1();
                        }
                    }
                }
                ");

                // ---------------------------------------------------------------------
                // Class;!@(foo)'^(Library1.csproj
                // ---------------------------------------------------------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory(
                    @"SLN;!@(foo)'^1\Class;!@(foo)'^(Library1\Class;!@(foo)'^(Library1.csproj",

                    @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                        <ProductVersion>8.0.50510</ProductVersion>
                        <SchemaVersion>2.0</SchemaVersion>
                        <ProjectGuid>{0B4B78CC-C752-43C2-BE9A-319D20216129}</ProjectGuid>
                        <OutputType>Library</OutputType>
                        <AppDesignerFolder>Properties</AppDesignerFolder>
                        <RootNamespace>Class____foo____Library1</RootNamespace>
                        <AssemblyName>Class%3b!%40%28foo%29%27^%28Library1</AssemblyName>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                        <DebugSymbols>true</DebugSymbols>
                        <DebugType>full</DebugType>
                        <Optimize>false</Optimize>
                        <OutputPath>bin\Debug\</OutputPath>
                        <DefineConstants>DEBUG;TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                        <DebugType>pdbonly</DebugType>
                        <Optimize>true</Optimize>
                        <OutputPath>bin\Release\</OutputPath>
                        <DefineConstants>TRACE</DefineConstants>
                        <ErrorReport>prompt</ErrorReport>
                        <WarningLevel>4</WarningLevel>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include=`System` />
                        <Reference Include=`System.Data` />
                        <Reference Include=`System.Xml` />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=`Class1.cs` />
                    </ItemGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

                // ---------------------------------------------------------------------
                // Class1.cs
                // ---------------------------------------------------------------------
                ObjectModelHelpers.CreateFileInTempProjectDirectory(
                    @"SLN;!@(foo)'^1\Class;!@(foo)'^(Library1\Class1.cs",

                    @"
                namespace Class____foo____Library1
                {
                    public class Class1
                    {
                    }
                }
                ");

                // Cons.ole;!@(foo)'^(Application1
                string targetForFirstProject = "Cons_ole_!__foo__^_Application1";

                MockLogger log = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln", new string[] { targetForFirstProject }, null, log);

                Assert.True(File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\bin\debug\Console;!@(foo)'^(Application1.exe"))); // @"Did not find expected file Console;!@(foo)'^(Application1.exe"
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalOverrideTaskHostVariable);
            }
        }
    }
#endif

    internal sealed class EscapingInProjectsHelper
    {
        /// <summary>
        /// Deletes all *.weirdo files from the temp path, and dumps 3 files there --
        /// a.weirdo, b.weirdo, c.weirdo.  This is so that we can exercise our wildcard
        /// matching a little bit without having to plumb mock objects all the way through
        /// the engine.
        /// </summary>
        internal static void CreateThreeWeirdoFiles()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // Create 3 files in the temp path -- a.weirdo, b.weirdo, and c.weirdo.
            File.WriteAllText(Path.Combine(ObjectModelHelpers.TempProjectDir, "a.weirdo"), String.Empty);
            File.WriteAllText(Path.Combine(ObjectModelHelpers.TempProjectDir, "b.weirdo"), String.Empty);
            File.WriteAllText(Path.Combine(ObjectModelHelpers.TempProjectDir, "c.weirdo"), String.Empty);
        }

        /// <summary>
        /// Given a project and an item type, gets the items of that type, and renames an item
        /// with the old evaluated include to have the new evaluated include instead.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="itemType"></param>
        /// <param name="oldEvaluatedInclude"></param>
        /// <param name="newEvaluatedInclude"></param>
        internal static IEnumerable<ProjectItem> ModifyItemOfTypeInProject(Project project, string itemType, string oldEvaluatedInclude, string newEvaluatedInclude)
        {
            IEnumerable<ProjectItem> itemsToMatch = project.GetItems(itemType);
            List<ProjectItem> matchingItems = new List<ProjectItem>();

            foreach (ProjectItem item in itemsToMatch)
            {
                if (String.Equals(item.EvaluatedInclude, oldEvaluatedInclude, StringComparison.OrdinalIgnoreCase))
                {
                    matchingItems.Add(item);
                }
            }

            for (int i = 0; i < matchingItems.Count; i++)
            {
                matchingItems[i].Rename(newEvaluatedInclude);
            }

            return matchingItems;
        }

        /// <summary>
        /// Helper for SpecialCharactersInMetadataValue tests
        /// </summary>
        internal static void SpecialCharactersInMetadataValueTests(ProjectItem item)
        {
            Assert.Equal("%3B", item.GetMetadata("EscapedSemicolon").UnevaluatedValue);
            Assert.Equal("%3B", item.GetMetadata("EscapedSemicolon").EvaluatedValueEscaped);
            Assert.Equal(";", item.GetMetadata("EscapedSemicolon").EvaluatedValue);
            Assert.Equal("%3B", Project.GetMetadataValueEscaped(item, "EscapedSemicolon"));
            Assert.Equal(";", item.GetMetadataValue("EscapedSemicolon"));

            Assert.Equal("%24", item.GetMetadata("EscapedDollarSign").UnevaluatedValue);
            Assert.Equal("%24", item.GetMetadata("EscapedDollarSign").EvaluatedValueEscaped);
            Assert.Equal("$", item.GetMetadata("EscapedDollarSign").EvaluatedValue);
            Assert.Equal("%24", Project.GetMetadataValueEscaped(item, "EscapedDollarSign"));
            Assert.Equal("$", item.GetMetadataValue("EscapedDollarSign"));
        }
    }
}
