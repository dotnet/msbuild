// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using NUnit.Framework;

using Microsoft.Build.UnitTests;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.UnitTests.Project_Tests;

namespace Microsoft.Build.UnitTests.EscapingInProjects_Tests
{
    /// <summary>
    /// Test task that just logs the parameters it receives.
    /// </summary>
    /// <owner>RGoel</owner>
    public class MyTestTask : Task
    {
        private ITaskItem taskItemParam;
        public ITaskItem TaskItemParam
        {
            get
            {
                return taskItemParam;
            }

            set
            {
                taskItemParam = value;
            }
        }

        override public bool Execute()
        {
            if (TaskItemParam != null)
            {
                Log.LogMessageFromText("Received TaskItemParam: " + TaskItemParam.ItemSpec, MessageImportance.High);
            }

            return true;
        }
    }

    [TestFixture]
    public class SimpleScenarios
    {
        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// a string parameter of a task, in this case the Message task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SemicolonInPropertyPassedIntoStringParam()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>
                    <Target Name=`Build`>
                        <Message Text=`Property value is '$(MyPropertyWithSemicolons)'` />
                    </Target>
                </Project>
                ");

            logger.AssertLogContains("Property value is 'abc ; def ; ghi'");
        }

        /// <summary>
        /// Make sure I can define a property with escaped characters and pass it into
        /// an ITaskItem[] task parameter.
        /// </summary>
        [Test]
        public void SemicolonInPropertyPassedIntoITaskItemParam()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <UsingTask TaskName=`Microsoft.Build.UnitTests.EscapingInProjects_Tests.MyTestTask` AssemblyFile=`{0}` />

                    <PropertyGroup>
                        <MyPropertyWithSemicolons>abc %3b def %3b ghi</MyPropertyWithSemicolons>
                    </PropertyGroup>

                    <Target Name=`Build`>
                        <MyTestTask TaskItemParam=`123 $(MyPropertyWithSemicolons) 789` />
                    </Target>

                </Project>

                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            logger.AssertLogContains("Received TaskItemParam: 123 abc ; def ; ghi 789");
        }

        /// <summary>
        /// If I try to add a new item to a project, and my new item's Include has an unescaped semicolon
        /// in it, then we shouldn't try to match it up against any existing wildcards.  This is a really
        /// bizarre scenario ... the caller probably meant to escape the semicolon.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void AddNewItemWithSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                        <MyWildCard Include=`foo;bar.weirdo`/>
                    </ItemGroup>
                </Project>
                ";

            BuildItem newItem = AddItem.AddNewItemHelper(projectOriginalContents,
                projectNewExpectedContents, "MyWildCard", "foo;bar.weirdo");

            Assertion.AssertEquals("Newly added item should have correct ItemName", "MyWildCard", newItem.Name);
            Assertion.AssertEquals("Newly added item should have correct Include", "foo;bar.weirdo", newItem.Include);
        }

        /// <summary>
        /// If I try to add a new item to a project, and my new item's Include has a property that
        /// contains an unescaped semicolon in it, then we shouldn't try to match it up against any existing 
        /// wildcards.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void AddNewItemWithPropertyContainingSemicolon()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
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
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>
                    <ItemGroup>
                        <MyWildCard Include=`$(FilenameWithSemicolon).weirdo`/>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";

            BuildItem newItem = AddItem.AddNewItemHelper(projectOriginalContents,
                projectNewExpectedContents, "MyWildCard", "$(FilenameWithSemicolon).weirdo");

            Assertion.AssertEquals("Newly added item should have correct ItemName", "MyWildCard", newItem.Name);
            Assertion.AssertEquals("Newly added item should have correct Include", "$(FilenameWithSemicolon).weirdo", newItem.Include);
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has an unescaped semicolon
        /// in it, then we shouldn't try to match it up against any existing wildcards.  This is a really
        /// bizarre scenario ... the caller probably meant to escape the semicolon.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ModifyItemIncludeSemicolon()
        {
            // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
            ModifyItem.CreateThreeWeirdoFilesHelper();
            
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @" 
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>
                
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @" 
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <MyWildcard Include=`a.weirdo` />
                        <MyWildcard Include=`foo;bar.weirdo` />
                        <MyWildcard Include=`c.weirdo` />
                    </ItemGroup>
                
                </Project>
                ";

            // Change b.weirdo to foo;bar.weirdo.
            ModifyItem.ModifyItemIncludeHelper(projectOriginalContents, projectNewExpectedContents,
                "b.weirdo", "foo;bar.weirdo");

            ModifyItem.CleanupWeirdoFilesHelper();
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has an escaped semicolon
        /// in it, and it matches the existing wildcard, then we shouldn't need to modify the project file.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ModifyItemIncludeEscapedSemicolon()
        {
            // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
            ModifyItem.CreateThreeWeirdoFilesHelper();
            
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @" 
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>
                
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @" 
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <MyWildcard Include=`*.weirdo` />
                    </ItemGroup>
                
                </Project>
                ";

            // Change b.weirdo to foo;bar.weirdo.
            ModifyItem.ModifyItemIncludeHelper(projectOriginalContents, projectNewExpectedContents,
                "b.weirdo", "foo%253Bbar.weirdo");

            ModifyItem.CleanupWeirdoFilesHelper();
        }

        /// <summary>
        /// If I try to modify an item in a project, and my new item's Include has a property that
        /// contains an unescaped semicolon in it, then we shouldn't try to match it up against any existing 
        /// wildcards.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ModifyItemAddPropertyContainingSemicolon()
        {
            // Populate the project directory with three physical files on disk -- a.weirdo, b.weirdo, c.weirdo.
            ModifyItem.CreateThreeWeirdoFilesHelper();
            
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @" 
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

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
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <PropertyGroup>
                        <FilenameWithSemicolon>foo;bar</FilenameWithSemicolon>
                    </PropertyGroup>

                    <ItemGroup>
                        <MyWildcard Include=`a.weirdo` />
                        <MyWildcard Include=`$(FileNameWithSemicolon).weirdo` />
                        <MyWildcard Include=`c.weirdo` />
                    </ItemGroup>
                
                </Project>
                ";

            // Change b.weirdo to foo;bar.weirdo.
            ModifyItem.ModifyItemIncludeHelper(projectOriginalContents, projectNewExpectedContents,
                "b.weirdo", "$(FileNameWithSemicolon).weirdo");

            ModifyItem.CleanupWeirdoFilesHelper();
        }

        /// <summary>
        /// Make sure that character escaping works as expected when adding a new item that matches
        /// an existing wildcarded item in the project file.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void AddNewItemThatMatchesWildcard1()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.weirdo`/>
                    </ItemGroup>
                </Project>
                ";

            BuildItem newItem = AddItem.AddNewItemHelper(projectOriginalContents,
                projectNewExpectedContents, "MyWildCard", "foo%253bbar.weirdo");

            Assertion.AssertEquals("Newly added item should have correct ItemName", "MyWildCard", newItem.Name);
            Assertion.AssertEquals("Newly added item should have correct Include", "*.weirdo", newItem.Include);
            Assertion.AssertEquals("Newly added item should have correct FinalItemSpec", "foo%253bbar.weirdo", newItem.FinalItemSpecEscaped);
            Assertion.AssertEquals("Newly added item should have correct FinalItemSpec", "foo%3bbar.weirdo", newItem.FinalItemSpec);
        }

        /// <summary>
        /// Make sure that character escaping works as expected when adding a new item that matches
        /// an existing wildcarded item in the project file.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void AddNewItemThatMatchesWildcard2()
        {
            // ************************************
            //               BEFORE
            // ************************************
            string projectOriginalContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.AAA%253bBBB`/>
                    </ItemGroup>
                </Project>
                ";


            // ************************************
            //               AFTER
            // ************************************
            string projectNewExpectedContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <MyWildCard Include=`*.AAA%253bBBB`/>
                    </ItemGroup>
                </Project>
                ";

            BuildItem newItem = AddItem.AddNewItemHelper(projectOriginalContents,
                projectNewExpectedContents, "MyWildCard", "foobar.AAA%253bBBB");

            Assertion.AssertEquals("Newly added item should have correct ItemName", "MyWildCard", newItem.Name);
            Assertion.AssertEquals("Newly added item should have correct Include", "*.AAA%253bBBB", newItem.Include);
            Assertion.AssertEquals("Newly added item should have correct FinalItemSpec", "foobar.AAA%253bBBB", newItem.FinalItemSpecEscaped);
            Assertion.AssertEquals("Newly added item should have correct FinalItemSpec", "foobar.AAA%3bBBB", newItem.FinalItemSpec);
        }

        /// <summary>
        /// Make sure that all inferred task outputs (those that are determined without actually
        /// executing the task) are left escaped when they become real items in the engine, and
        /// they only get unescaped when fed into a subsequent task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InferEscapedOutputsFromTask()
        {
            string inputFile = ObjectModelHelpers.CreateTempFileOnDisk("");
            string outputFile = ObjectModelHelpers.CreateTempFileOnDisk("");

            try
            {
                MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`GenerateResources` Inputs=`{0}` Outputs=`{1}`>
                        <NonExistentTask OutputResources=`aaa%253bbbb.resx; ccc%253bddd.resx`>
                            <Output ItemName=`Resource` TaskParameter=`OutputResources`/>
                        </NonExistentTask>
                    </Target>

                    <Target Name=`Build` DependsOnTargets=`GenerateResources`>
                        <Message Text=`Resources = @(Resource)`/>
                    </Target>

                </Project>

                ", inputFile, outputFile));

                logger.AssertLogContains("Resources = aaa%3bbbb.resx;ccc%3bddd.resx");
            }
            finally
            {
                File.Delete(inputFile);
                File.Delete(outputFile);
            }
        }

        /// <summary>
        /// Do an item transform, where the transform expression contains an unescaped semicolon as well
        /// as an escaped percent sign.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ItemTransformContainingSemicolon()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <TextFile Include=`X.txt`/>
                        <TextFile Include=`Y.txt`/>
                        <TextFile Include=`Z.txt`/>
                    </ItemGroup>
                    <Target Name=`Build`>
                        <Message Text=`Transformed item list: '@(TextFile->'%(FileName);%(FileName)%253b%(FileName)%(Extension)','    ')'` />
                    </Target>
                </Project>

                ");

            logger.AssertLogContains("Transformed item list: 'X;X%3bX.txt    Y;Y%3bY.txt    Z;Z%3bZ.txt'");
        }

        /// <summary>
        /// Test that we can pass in global properties containing escaped characters.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GlobalPropertyWithEscapedCharacters()
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`MyGlobalProperty = '$(MyGlobalProperty)'` />
                    </Target>
                </Project>
                ", logger);

            project.GlobalProperties.SetProperty("MyGlobalProperty", "foo%253bbar");

            bool success = project.Build(null, null);
            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            logger.AssertLogContains("MyGlobalProperty = 'foo%3bbar'");
        }
    }

    [TestFixture]
    public class FullProjectsUsingMicrosoftCommonTargets
    {
        /// <summary>
        /// Regression test for bug VSWhidbey 282492:
        ///     ESCAPING: Escaping in conditionals is broken.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SemicolonInConfiguration()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
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

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("foo.csproj", logger);

            // Build the default targets using the Configuration "a;b'c".
            project.GlobalProperties.SetProperty("Configuration", "a;b'c", true /* literal value */);
            bool success = project.Build(null, null);
            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"obj\a;b'c\ClassLibrary16.dll");
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bin\a;b'c\ClassLibrary16.dll");

            logger.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\a;b'c\ClassLibrary16.dll")));
        }

        /// <summary>
        /// Regression test for bug VSWhidbey 157204:
        ///     ESCAPING: CopyBuildTarget target fails if the output assembly name contains a semicolon or single-quote
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SemicolonInAssemblyName()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
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

            MockLogger log = ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj");

            Assertion.Assert(@"Did not find expected file obj\debug\Class;Library16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\Class;Library16.dll")));
            Assertion.Assert(@"Did not find expected file obj\debug\Class;Library16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\Class;Library16.pdb")));
            Assertion.Assert(@"Did not find expected file bin\debug\Class;Library16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\Class;Library16.dll")));
            Assertion.Assert(@"Did not find expected file bin\debug\Class;Library16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\Class;Library16.pdb")));

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class;Library16.dll")));
        }

        /// <summary>
        /// Regression test for bug VSWhidbey 157236:
        ///     ESCAPING: Conversion Issue: Properties with $(xxx) as literals are not being converted correctly
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void DollarSignInAssemblyName()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
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

            MockLogger log = ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj");

            Assertion.Assert(@"Did not find expected file obj\debug\Class$(prop)Library16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\Class$(prop)Library16.dll")));
            Assertion.Assert(@"Did not find expected file obj\debug\Class$(prop)Library16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\Class$(prop)Library16.pdb")));
            Assertion.Assert(@"Did not find expected file bin\debug\Class$(prop)Library16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\Class$(prop)Library16.dll")));
            Assertion.Assert(@"Did not find expected file bin\debug\Class$(prop)Library16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\Class$(prop)Library16.pdb")));

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\Class$(prop)Library16.dll")));
        }

        /// <summary>
        /// Regression test for bug VSWhidbey 146010 and related bugs.  This is the case when one of the
        /// source code files in the project has a filename containing a semicolon.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SemicolonInSourceCodeFilename()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------
            // Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.csproj", @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                        <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
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

            MockLogger log = ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.csproj");

            Assertion.Assert(@"Did not find expected file obj\debug\ClassLibrary16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\ClassLibrary16.dll")));
            Assertion.Assert(@"Did not find expected file obj\debug\ClassLibrary16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"obj\debug\ClassLibrary16.pdb")));
            Assertion.Assert(@"Did not find expected file bin\debug\ClassLibrary16.dll",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\ClassLibrary16.dll")));
            Assertion.Assert(@"Did not find expected file bin\debug\ClassLibrary16.pdb",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\ClassLibrary16.pdb")));

            log.AssertLogContains(String.Format("foo -> {0}", Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\Debug\ClassLibrary16.dll")));
        }

        /// <summary>
        /// Build a .SLN file using MSBuild.  The .SLN and the projects contained within
        /// have all sorts of crazy characters in their name (courtesy of DanMose who apparently
        /// just ran his fingers up and down the on the upper row of his keyboard :) ).  There
        /// is even a P2P reference between the two projects in the .SLN.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SolutionWithLotsaCrazyCharacters()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35) == null)
            {
                Assert.Ignore(".NET Framework 3.5 is required to be installed for this test, but it is not installed.");
            }

            ObjectModelHelpers.DeleteTempProjectDirectory();

            // ---------------------------------------------------------------------
            // Console;!@(foo)'^(Application1.sln
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln",

                @"Microsoft Visual Studio Solution File, Format Version 10.00
                # Visual Studio 2005
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
                ");

            // ---------------------------------------------------------------------
            // Console;!@(foo)'^(Application1.csproj
            // ---------------------------------------------------------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\Cons.ole;!@(foo)'^(Application1.csproj",

                @"
                <Project DefaultTargets=`Build` ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
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
                <Project DefaultTargets=`Build` ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
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

                    <!-- The old OM, which is what this solution is being built under, doesn't understand
                         BeforeTargets, so this test was failing, because _AssignManagedMetadata was set 
                         up as a BeforeTarget for Build.  Copied here so that build will return the correct
                         information again. -->
                    <Target Name=`BeforeBuild`>
                        <ItemGroup>
                            <BuiltTargetPath Include=`$(TargetPath)`>
                                <ManagedAssembly>$(ManagedAssembly)</ManagedAssembly>
                            </BuiltTargetPath>
                        </ItemGroup>
                    </Target>
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
            MockLogger log = ObjectModelHelpers.BuildTempProjectFileWithTargetsExpectSuccess(@"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1.sln", new string[] { targetForFirstProject }, new BuildPropertyGroup());

            Assertion.Assert(@"Did not find expected file Console;!@(foo)'^(Application1.exe",
                File.Exists(Path.Combine(ObjectModelHelpers.TempProjectDir, 
                @"SLN;!@(foo)'^1\Console;!@(foo)'^(Application1\bin\debug\Console;!@(foo)'^(Application1.exe")));
        }
    }
}
