// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public sealed class MSBuildTask_Tests : IDisposable
    {
        private readonly ITestOutputHelper _testOutput;

        public MSBuildTask_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// If we pass in an item spec that is over the max path but it can be normalized down to something under the max path, we should still work and not
        /// throw a path too long exception
        /// </summary>
        [Fact]
        [ActiveIssue("https://github.com/Microsoft/msbuild/issues/4247")]
        public void ProjectItemSpecTooLong()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());

                string tempPath = Path.GetTempPath();

                string tempProject = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB; TargetC` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`TargetA` Outputs=`a1.dll`/>
                    <Target Name=`TargetB` Outputs=`b1.dll; b2.dll`/>
                    <Target Name=`TargetC` Outputs=`@(C_Outputs)`>
                        <CreateItem Include=`c1.dll` AdditionalMetadata=`MSBuildSourceProjectFile=birch; MSBuildSourceTargetName=oak`>
                            <Output ItemName=`C_Outputs` TaskParameter=`Include`/>
                        </CreateItem>
                    </Target>
                </Project>
                ");

                string fileName = Path.GetFileName(tempProject);

                string projectFile1 = null;
                for (int i = 0; i < 250; i++)
                {
                    projectFile1 += "..\\";
                }

                int rootLength = Path.GetPathRoot(tempPath).Length;
                string tempPathNoRoot = tempPath.Substring(rootLength);

                projectFile1 += Path.Combine(tempPathNoRoot, fileName);

                string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`Build`>
                        <MSBuild Projects=`" + projectFile1 + @"` />
                    </Target>
                </Project>";
                try
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);

                    bool success = p.Build();
                    Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."
                }
                finally
                {
                    File.Delete(tempProject);
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        /// <summary>
        /// Ensure that the MSBuild task tags any output items with two pieces of metadata -- MSBuildSourceProjectFile and
        /// MSBuildSourceTargetName  -- that give an indication of where the items came from.
        /// </summary>
        [Fact]
        public void OutputItemsAreTaggedWithProjectFileAndTargetName()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB; TargetC` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`TargetA` Outputs=`a1.dll`/>
                    <Target Name=`TargetB` Outputs=`b1.dll; b2.dll`/>
                    <Target Name=`TargetC` Outputs=`@(C_Outputs)`>
                        <CreateItem Include=`c1.dll`>
                            <Output ItemName=`C_Outputs` TaskParameter=`Include`/>
                        </CreateItem>
                    </Target>
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`TargetG` Outputs=`g1.dll; g2.dll`/>
                    <Target Name=`TargetH` Outputs=`h1.dll`/>
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"` />
                        <Projects Include=`" + projectFile2 + @"` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);

                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    a1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetA
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    b2.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    c1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetC
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    g2.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(7, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Ensures that it is possible to call the MSBuild task with an empty Projects parameter, and it 
        /// shouldn't error, and it shouldn't try to build itself.
        /// </summary>
        [Fact]
        public void EmptyProjectsParameterResultsInNoop()
        {
            string projectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=` @(empty) ` />
                    </Target>
                </Project>
                ";

            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build();
            Assert.True(success); // "Build failed.  See test output (Attachments in Azure Pipelines) for details"
        }

        /// <summary>
        /// Verifies that nonexistent projects aren't normally skipped
        /// </summary>
        [Fact]
        public void NormallyDoNotSkipNonexistentProjects()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentProjectsMain.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=`this_project_does_not_exist.csproj` />
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"SkipNonexistentProjectsMain.csproj", logger);
            string error = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFound"), "this_project_does_not_exist.csproj");
            Assert.Contains(error, logger.FullLog);
        }

        /// <summary>
        /// Verifies that nonexistent projects aren't normally skipped
        /// </summary>
        [Fact]
        public void NormallyDoNotSkipNonexistentProjectsBuildInParallel()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentProjectsMain.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=`this_project_does_not_exist.csproj` BuildInParallel=`true`/>
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"SkipNonexistentProjectsMain.csproj", logger);
            string error = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFound"), "this_project_does_not_exist.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(1, logger.ErrorCount);
            Assert.Contains(error, logger.FullLog);
        }

        /// <summary>
        /// Verifies that nonexistent projects are skipped when requested
        /// </summary>
        [Fact]
        public void SkipNonexistentProjects()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentProjectsMain.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=`this_project_does_not_exist.csproj;foo.csproj` SkipNonexistentProjects=`true` />
                    </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "foo.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <Message Text=`Hello from foo.csproj`/>
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"SkipNonexistentProjectsMain.csproj", logger);

            logger.AssertLogContains("Hello from foo.csproj");
            string message = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFoundMessage"), "this_project_does_not_exist.csproj");
            string error = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFound"), "this_project_does_not_exist.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(0, logger.ErrorCount);
            Assert.Contains(message, logger.FullLog); // for the missing project
            Assert.DoesNotContain(error, logger.FullLog);
        }

        /// <summary>
        /// Verifies that nonexistent projects are skipped when requested when building in parallel.
        /// DDB # 125831
        /// </summary>
        [Fact]
        public void SkipNonexistentProjectsBuildingInParallel()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentProjectsMain.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=`this_project_does_not_exist.csproj;foo.csproj` SkipNonexistentProjects=`true` BuildInParallel=`true` />
                    </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "foo.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <Message Text=`Hello from foo.csproj`/>
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"SkipNonexistentProjectsMain.csproj", logger);

            logger.AssertLogContains("Hello from foo.csproj");
            string message = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFoundMessage"), "this_project_does_not_exist.csproj");
            string error = String.Format(AssemblyResources.GetString("MSBuild.ProjectFileNotFound"), "this_project_does_not_exist.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(0, logger.ErrorCount);
            Assert.Contains(message, logger.FullLog); // for the missing project
            Assert.DoesNotContain(error, logger.FullLog);
        }

        [Fact]
        public void LogErrorWhenBuildingVCProj()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "BuildingVCProjMain.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <MSBuild Projects=`blah.vcproj;foo.csproj` StopOnFirstFailure=`false` />
                    </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "foo.csproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <Message Text=`Hello from foo.csproj`/>
                    </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "blah.vcproj",
                @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <NotWellFormedMSBuildFormatTag />
                    <Target Name=`t` >
                        <Message Text=`Hello from blah.vcproj`/>
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"BuildingVCProjMain.csproj", logger);

            logger.AssertLogContains("Hello from foo.csproj");
            string error = String.Format(AssemblyResources.GetString("MSBuild.ProjectUpgradeNeededToVcxProj"), "blah.vcproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(1, logger.ErrorCount);
            Assert.Contains(error, logger.FullLog);
        }

#if FEATURE_COMPILE_IN_TESTS
        /// <summary>
        /// Regression test for bug 533369.  Calling the MSBuild task, passing in a property
        /// in the Properties parameter that has a special character in its value, such as semicolon.
        /// However, it's a situation where the project author doesn't have control over the
        /// property value and so he can't escape it himself.
        /// </summary>
        [Fact]
        public void PropertyOverridesContainSemicolon()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // -------------------------------------------------------
            // ConsoleApplication1.csproj
            // -------------------------------------------------------

            // Just a normal console application project.
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"bug'533'369\Sub;Dir\ConsoleApplication1\ConsoleApplication1.csproj", @"

                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                    <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                    <OutputType>Exe</OutputType>
                    <AssemblyName>ConsoleApplication1</AssemblyName>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                  </PropertyGroup>
                  <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                    <DebugType>pdbonly</DebugType>
                    <Optimize>true</Optimize>
                    <OutputPath>bin\Release\</OutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=`System` />
                    <Reference Include=`System.Data` />
                    <Reference Include=`System.Xml` />
                  </ItemGroup>
                  <ItemGroup>
                    <Compile Include=`Program.cs` />
                  </ItemGroup>
                  <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
                ");

            // -------------------------------------------------------
            // Program.cs
            // -------------------------------------------------------

            // Just a normal console application project.
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"bug'533'369\Sub;Dir\ConsoleApplication1\Program.cs", @"
                using System;

                namespace ConsoleApplication32
                {
                    class Program
                    {
                        static void Main(string[] args)
                        {
                            Console.WriteLine(`Hello world`);
                        }
                    }
                }
                ");


            // -------------------------------------------------------
            // TeamBuild.proj
            // -------------------------------------------------------
            // Attempts to build the above ConsoleApplication1.csproj by calling the MSBuild task, 
            // and overriding the OutDir property.  However, the value being passed into OutDir
            // is coming from another property which is produced by CreateProperty and has
            // some special characters in it.
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                @"bug'533'369\Sub;Dir\TeamBuild.proj", @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`Build`>
                    
                        <CreateProperty Value=`$(MSBuildProjectDirectory)\binaries\`>
                            <Output PropertyName=`MasterOutDir` TaskParameter=`Value`/>
                        </CreateProperty>
                        
                        <MSBuild Projects=`ConsoleApplication1\ConsoleApplication1.csproj`
                                 Properties=`OutDir=$(MasterOutDir)`
                                 Targets=`Rebuild`/>
                    </Target>
                    
                </Project>
                ");

            MockLogger logger = new MockLogger(_testOutput);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"bug'533'369\Sub;Dir\TeamBuild.proj", logger);

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(@"bug'533'369\Sub;Dir\binaries\ConsoleApplication1.exe");
        }
#endif

        /// <summary>
        /// Check if passing different global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentGlobalPropertiesWithDefault()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyProp)'=='0'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyProp)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyProp)'=='0'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyProp)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"` />
                        <Projects Include=`" + projectFile1 + @"`>
                            <Properties>MyProp=1</Properties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"` />
                        <Projects Include=`" + projectFile2 + @"`>
                            <Properties>MyProp=1</Properties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)` Properties=`MyProp=0`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    a1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetA
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(4, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Check if passing different global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentGlobalPropertiesWithoutDefault()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyProp)'=='0'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyProp)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyProp)'=='0'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyProp)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"` />
                        <Projects Include=`" + projectFile1 + @"`>
                            <Properties>MyProp=1</Properties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"` />
                        <Projects Include=`" + projectFile2 + @"`>
                            <Properties>MyProp=1</Properties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(2, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Check trailing semicolons are ignored
        /// </summary>
        [Fact]
        public void VariousPropertiesToMSBuildTask()
        {
            string projectFile = null;

            try
            {
                projectFile = ObjectModelHelpers.CreateTempFileOnDisk(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <ItemGroup>
                        <PR Include='$(MSBuildProjectFullPath)'>
                          <Properties>a=a;b=b;</Properties>
                          <AdditionalProperties>e=e;g=1;f=f;</AdditionalProperties>
                          <UndefineProperties>g;h;</UndefineProperties>
                        </PR>
                      </ItemGroup>
                      <Target Name='a'> 
                        <MSBuild Projects='@(PR)' Properties='c=c;d=d;' RemoveProperties='i;c;' Targets='b'/>
                      </Target>
                      <Target Name='b'>
                        <Message Text='a=[$(a)]' Importance='High' />
                        <Message Text='b=[$(b)]' Importance='High' />
                        <Message Text='c=[$(c)]' Importance='High' />
                        <Message Text='d=[$(d)]' Importance='High' />
                        <Message Text='e=[$(e)]' Importance='High' />
                        <Message Text='f=[$(f)]' Importance='High' />
                        <Message Text='g=[$(g)]' Importance='High' />
                      </Target>
                    </Project>
                ");

                MockLogger logger = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess(projectFile, logger);

                Console.WriteLine(logger.FullLog);

                logger.AssertLogContains("a=[a]");
                logger.AssertLogContains("b=[b]");
                logger.AssertLogContains("c=[]");
                logger.AssertLogContains("d=[]");
                logger.AssertLogContains("e=[e]");
                logger.AssertLogContains("f=[f]");
                logger.AssertLogContains("g=[]");
            }
            finally
            {
                File.Delete(projectFile);
            }
        }

        /// <summary>
        /// Check if passing different global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentGlobalPropertiesWithBlanks()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyProp)'=='0'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyProp)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyProp)'=='0'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyProp)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"` />
                        <Projects Include=`" + projectFile1 + @"`>
                            <Properties></Properties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"` />
                        <Projects Include=`" + projectFile2 + @"`>
                            <Properties>MyProp=1</Properties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    h1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetH
                    ", projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Single(targetOutputs["Build"].Items);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }


        /// <summary>
        /// Check if passing different global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentGlobalPropertiesInvalid()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyProp)'=='0'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyProp)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyProp)'=='0'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyProp)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"` />
                        <Projects Include=`" + projectFile1 + @"`>
                            <Properties>=1</Properties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"` />
                        <Projects Include=`" + projectFile2 + @"`>
                            <Properties>=;1</Properties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                bool success = p.Build();
                Assert.False(success); // "Build succeeded.  See 'Standard Out' tab for details."
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Check if passing additional global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentAdditionalPropertiesWithDefault()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyPropG)'=='1'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyPropA)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyPropG)'=='1'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyPropA)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"`>
                            <AdditionalProperties>MyPropA=1</AdditionalProperties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"`>
                            <AdditionalProperties>MyPropA=0</AdditionalProperties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)` Properties=`MyPropG=1`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    a1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetA
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(3, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }


        /// <summary>
        /// Check if passing additional global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentAdditionalPropertiesWithGlobalProperties()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyPropG)'=='0'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyPropA)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyPropG)'=='0'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyPropA)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"`>
                            <Properties>MyPropG=1</Properties>
                            <AdditionalProperties>MyPropA=1</AdditionalProperties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"`>
                            <Properties>MyPropG=0</Properties>
                            <AdditionalProperties>MyPropA=1</AdditionalProperties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)` Properties=`MyPropG=1`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(3, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }


        /// <summary>
        /// Check if passing additional global properties via metadata works
        /// </summary>
        [Fact]
        public void DifferentAdditionalPropertiesWithoutDefault()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetA; TargetB` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>

                    <Target Name=`TargetA` Outputs=`a1.dll` Condition=`'$(MyPropG)'=='1'`/>
                    <Target Name=`TargetB` Outputs=`b1.dll` Condition=`'$(MyPropA)'=='1'`/>
                   
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`TargetG; TargetH` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`TargetG` Outputs=`g1.dll` Condition=`'$(MyPropG)'=='1'` />
                    <Target Name=`TargetH` Outputs=`h1.dll` Condition=`'$(MyPropA)'=='1'` />
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile1 + @"`>
                            <AdditionalProperties>MyPropA=1</AdditionalProperties>
                        </Projects>
                        <Projects Include=`" + projectFile2 + @"`>
                            <AdditionalProperties>MyPropA=1</AdditionalProperties>
                        </Projects>
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(2, targetOutputs["Build"].Items.Length);
                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, targetOutputs["Build"].Items, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Properties and Targets that use non-standard separation chars
        /// </summary>
        [Fact]
        public void TargetsWithSeparationChars()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`Build` xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`Clean` />
                    <Target Name=`Build` />
                    <Target Name=`BuildAgain` />
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`Build` xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <Targets>Clean%3BBuild%3CBuildAgain</Targets>
                    </PropertyGroup>

                    <ItemGroup>
                        <ProjectFile Include=`" + projectFile1 + @"` />
                    </ItemGroup>
                   
                    <Target Name=`Build` Outputs=`$(SomeOutputs)`>
                        <MSBuild Projects=`@(ProjectFile)` Targets=`$(Targets)` TargetAndPropertyListSeparators=`%3B;%3C` />
                    </Target>
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile2 + @"` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                bool success = p.Build();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Verify stopOnFirstFailure with BuildInParallel override message are correctly logged
        /// Also verify stop on first failure will not build the second project if the first one failed
        /// The Aardvark tests which also test StopOnFirstFailure are at:
        /// qa\md\wd\DTP\MSBuild\ShippingExtensions\ShippingTasks\MSBuild\_Tst\MSBuild.StopOnFirstFailure
        /// </summary>
        [Fact]
        public void StopOnFirstFailureandBuildInParallelSingleNode()
        {
            string project1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                  <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                      <Target Name='msbuild'>
                          <Error Text='Error'/>
                      </Target>
                  </Project>
                  ");

            string project2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                   <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                       <Target Name='msbuild'>
                           <Message Text='SecondProject'/>
                       </Target>
                    </Project>
                  ");

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(project1), new TaskItem(project2)
                };

                // Test the various combinations of BuildInParallel and StopOnFirstFailure when the msbuild task is told there are not multiple nodes 
                // running in the system
                for (int i = 0; i < 4; i++)
                {
                    bool buildInParallel = false;
                    bool stopOnFirstFailure = false;

                    // first set up the project being built. 
                    switch (i)
                    {
                        case 0:
                            buildInParallel = true;
                            stopOnFirstFailure = true;
                            break;
                        case 1:
                            buildInParallel = true;
                            stopOnFirstFailure = false;
                            break;
                        case 2:
                            buildInParallel = false;
                            stopOnFirstFailure = true;
                            break;
                        case 3:
                            buildInParallel = false;
                            stopOnFirstFailure = false;
                            break;
                    }

                    string parentProjectContents = @"
                        <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                            <ItemGroup>
                                <Projects Include=`" + project1 + @"` />
                                <Projects Include=`" + project2 + @"` />
                            </ItemGroup>

                            <Target Name=`Build` Returns=`@(Outputs)`>
                                <MSBuild Projects=`@(Projects)` Targets=`msbuild` BuildInParallel=`" + buildInParallel.ToString() + @"` StopOnFirstFailure=`" + stopOnFirstFailure.ToString() + @"`>
                                    <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                                </MSBuild>
                            </Target>
                        </Project>";

                    MockLogger logger = new MockLogger();
                    Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents, logger);
                    bool success = p.Build(logger);
                    switch (i)
                    {
                        case 0:
                            // Verify setting BuildInParallel and StopOnFirstFailure to 
                            // true will cause the msbuild task to set BuildInParallel to false during the execute
                            // Verify build did not build second project which has the message SecondProject
                            logger.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                        case 1:
                            // Verify setting BuildInParallel to true and StopOnFirstFailure to 
                            // false will cause no change in BuildInParallel
                            // Verify build did  build second project which has the message SecondProject
                            logger.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                        case 2:
                            // Verify build did not build second project which has the message SecondProject
                            logger.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;

                        case 3:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to 
                            // false will cause no change in BuildInParallel
                            // Verify build did build second project which has the message SecondProject
                            logger.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                    }
                    // The build should fail as the first project has an error
                    Assert.False(success, "Iteration of i " + i + " Build Succeeded.  See 'Standard Out' tab for details.");
                }
            }
            finally
            {
                File.Delete(project1);
                File.Delete(project2);
            }
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Verify stopOnFirstFailure with BuildInParallel override message are correctly logged when there are multiple nodes
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void StopOnFirstFailureandBuildInParallelMultipleNode()
        {
            string project1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                  <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                      <Target Name='msbuild'>
                          <Error Text='Error'/>
                      </Target>
                  </Project>
                  ");

            string project2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                   <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                       <Target Name='msbuild'>
                           <Message Text='SecondProject'/>
                       </Target>
                    </Project>
                  ");

            try
            {
                // Test the various combinations of BuildInParallel and StopOnFirstFailure when the msbuild task is told there are multiple nodes 
                // running in the system
                for (int i = 0; i < 4; i++)
                {
                    bool buildInParallel = false;
                    bool stopOnFirstFailure = false;

                    // first set up the project being built. 
                    switch (i)
                    {
                        case 0:
                            buildInParallel = true;
                            stopOnFirstFailure = true;
                            break;
                        case 1:
                            buildInParallel = true;
                            stopOnFirstFailure = false;
                            break;
                        case 2:
                            buildInParallel = false;
                            stopOnFirstFailure = true;
                            break;
                        case 3:
                            buildInParallel = false;
                            stopOnFirstFailure = false;
                            break;
                    }

                    string parentProjectContents = @"
                        <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                            <ItemGroup>
                                <Projects Include=`" + project1 + @"` />
                                <Projects Include=`" + project2 + @"` />
                            </ItemGroup>

                            <Target Name=`Build` Returns=`@(Outputs)`>
                                <MSBuild Projects=`@(Projects)` Targets=`msbuild` BuildInParallel=`" + buildInParallel.ToString() + @"` StopOnFirstFailure=`" + stopOnFirstFailure.ToString() + @"`>
                                    <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                                </MSBuild>
                            </Target>
                        </Project>";

                    MockLogger logger = new MockLogger();
                    ProjectCollection pc = new ProjectCollection(null, new List<ILogger> { logger }, null, ToolsetDefinitionLocations.Default, 2, false);
                    Project p = ObjectModelHelpers.CreateInMemoryProject(pc, parentProjectContents, logger);
                    bool success = p.Build();
                    switch (i)
                    {
                        case 0:
                            // Verify build did build second project which has the message SecondProject
                            logger.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                        case 1:
                            // Verify setting BuildInParallel to true and StopOnFirstFailure to 
                            // false will cause no change in BuildInParallel
                            // Verify build did build second project which has the message SecondProject
                            logger.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                        case 2:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to 
                            // true will cause no change in BuildInParallel
                            // Verify build did not build second project which has the message SecondProject
                            logger.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;

                        case 3:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to 
                            // false will cause no change in BuildInParallel
                            // Verify build did build second project which has the message SecondProject
                            logger.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NoStopOnFirstFailure"));
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.NotBuildingInParallel"));
                            break;
                    }
                    // The build should fail as the first project has an error
                    Assert.False(success, "Iteration of i " + i + " Build Succeeded.  See 'Standard Out' tab for details.");
                }
            }
            finally
            {
                File.Delete(project1);
                File.Delete(project2);
            }
        }
#endif

        /// <summary>
        /// Test the skipping of the remaining projects. Verify the skip message is only displayed when there are projects to skip.
        /// </summary>
        [Fact]
        public void SkipRemainingProjects()
        {
            string project1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                  <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                      <Target Name='msbuild'>
                          <Error Text='Error'/>
                      </Target>
                  </Project>
                  ");

            string project2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                   <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                       <Target Name='msbuild'>
                           <Message Text='SecondProject'/>
                       </Target>
                    </Project>
                  ");

            try
            {
                string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + project1 + @"` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)` Targets=`msbuild` BuildInParallel=`false` StopOnFirstFailure=`true`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

                MockLogger logger = new MockLogger();
                ProjectCollection pc = new ProjectCollection(null, new List<ILogger> { logger }, null, ToolsetDefinitionLocations.Default, 2, false);
                Project p = ObjectModelHelpers.CreateInMemoryProject(pc, parentProjectContents, logger);
                bool success = p.Build();

                logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                Assert.False(success); // "Build Succeeded.  See 'Standard Out' tab for details."

                parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + project2 + @"` />
                        <Projects Include=`" + project1 + @"` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)` Targets=`msbuild` BuildInParallel=`false` StopOnFirstFailure=`true`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

                MockLogger logger2 = new MockLogger();
                Project p2 = ObjectModelHelpers.CreateInMemoryProject(pc, parentProjectContents, logger2);
                bool success2 = p2.Build();
                logger2.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingProjects"));
                Assert.False(success2); // "Build Succeeded.  See 'Standard Out' tab for details."
            }
            finally
            {
                File.Delete(project1);
                File.Delete(project2);
            }
        }

        /// <summary>
        /// Verify the behavior of Target execution with StopOnFirstFailure
        /// </summary>
        [Fact]
        public void TargetStopOnFirstFailureBuildInParallel()
        {
            string project1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                   <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                   <Target Name='T1'>
                          <Message Text='Proj2 T1 message'/>
                      </Target>
                    <Target Name='T2'>
                          <Message Text='Proj2 T2 message'/>
                      </Target>
                    <Target Name='T3'>
                           <Error Text='Error'/>
                      </Target>
                    </Project>
                  ");

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(project1)
                };
                for (int i = 0; i < 6; i++)
                {
                    bool stopOnFirstFailure = false;
                    bool runEachTargetSeparately = false;
                    string target1 = String.Empty;
                    string target2 = String.Empty;
                    string target3 = String.Empty;

                    switch (i)
                    {
                        case 0:
                            stopOnFirstFailure = true;
                            runEachTargetSeparately = true;
                            target1 = "T1";
                            target2 = "T2";
                            target3 = "T3";
                            break;
                        case 1:
                            stopOnFirstFailure = true;
                            runEachTargetSeparately = true;
                            target1 = "T1";
                            target2 = "T3";
                            target3 = "T2";
                            break;
                        case 2:
                            stopOnFirstFailure = false;
                            runEachTargetSeparately = true;
                            target1 = "T1";
                            target2 = "T3";
                            target3 = "T2";
                            break;
                        case 3:
                            stopOnFirstFailure = true;
                            target1 = "T1";
                            target2 = "T2";
                            target3 = "T3";
                            break;
                        case 4:
                            stopOnFirstFailure = true;
                            target1 = "T1";
                            target2 = "T3";
                            target3 = "T2";
                            break;
                        case 5:
                            stopOnFirstFailure = false;
                            target1 = "T1";
                            target2 = "T3";
                            target3 = "T2";
                            break;
                    }

                    string parentProjectContents = @"
                        <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                            <ItemGroup>
                                <Projects Include=`" + project1 + @"` />
                            </ItemGroup>

                            <ItemGroup>
                                <Targets Include=`" + target1 + @"` />
                                <Targets Include=`" + target2 + @"` />
                                <Targets Include=`" + target3 + @"` />
                            </ItemGroup>

                            <Target Name=`Build` Returns=`@(Outputs)`>
                                <MSBuild Projects=`@(Projects)` Targets=`@(Targets)` StopOnFirstFailure=`" + stopOnFirstFailure.ToString() + @"` RunEachTargetSeparately=`" + runEachTargetSeparately.ToString() + @"`>
                                    <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                                </MSBuild>
                            </Target>
                        </Project>";

                    MockLogger logger = new MockLogger();
                    Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents, logger);
                    bool success = p.Build();

                    switch (i)
                    {
                        case 0:
                            // Test the case where the error is in the last project and RunEachTargetSeparately = true
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogContains("Proj2 T2 message");
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            break;
                        case 1:
                            // Test the case where the error is in the second target out of 3.
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogContains(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            logger.AssertLogDoesntContain("Proj2 T2 message");
                            // The build should fail as the first project has an error
                            break;
                        case 2:
                            // Test case where error is in second last target but stopOnFirstFailure is false
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogContains("Proj2 T2 message");
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            break;
                        // Test the cases where RunEachTargetSeparately is false. In these cases all of the targets should be submitted at once
                        case 3:
                            // Test the case where the error is in the last project and RunEachTargetSeparately = true
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogContains("Proj2 T2 message");
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            // The build should fail as the first project has an error
                            break;
                        case 4:
                            // Test the case where the error is in the second target out of 3.
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogDoesntContain("Proj2 T2 message");
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            // The build should fail as the first project has an error
                            break;
                        case 5:
                            // Test case where error is in second last target but stopOnFirstFailure is false
                            logger.AssertLogContains("Proj2 T1 message");
                            logger.AssertLogDoesntContain("Proj2 T2 message");
                            logger.AssertLogDoesntContain(AssemblyResources.GetString("MSBuild.SkippingRemainingTargets"));
                            break;
                    }

                    // The build should fail as the first project has an error
                    Assert.False(success, "Iteration of i:" + i + "Build Succeeded.  See 'Standard Out' tab for details.");
                }
            }
            finally
            {
                File.Delete(project1);
            }
        }

        /// <summary>
        /// Properties and Targets that use non-standard separation chars
        /// </summary>
        [Fact]
        public void PropertiesWithSeparationChars()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`Build` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`Build` Outputs=`|$(a)|$(b)|$(C)|$(D)|` />
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`Build` xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <AValues>a%3BA</AValues>
                        <BValues>b;B</BValues>
                        <CValues>c;C</CValues>
                        <DValues>d%3BD</DValues>
                    </PropertyGroup>

                    <ItemGroup>
                        <ProjectFile Include=`" + projectFile1 + @"`>
                            <AdditionalProperties>C=$(CValues)%3BD=$(DValues)</AdditionalProperties>
                        </ProjectFile>
                    </ItemGroup>
                   
                    <Target Name=`Build` Outputs=`$(SomeOutputs)`>
                        <MSBuild Projects=`@(ProjectFile)` Targets=`Build` Properties=`a=$(AValues)%3Bb=$(BValues)` TargetAndPropertyListSeparators=`%3B`>
                            <Output TaskParameter=`TargetOutputs` PropertyName=`SomeOutputs`/>
                        </MSBuild>
                    </Target>	
                </Project>
                ");

            string parentProjectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <Projects Include=`" + projectFile2 + @"` />
                    </ItemGroup>

                    <Target Name=`Build` Returns=`@(Outputs)`>
                        <MSBuild Projects=`@(Projects)`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`Outputs` />
                        </MSBuild>
                    </Target>
                </Project>";

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile2)
                };

                Project p = ObjectModelHelpers.CreateInMemoryProject(parentProjectContents);
                ProjectInstance pi = p.CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = pi.Build(null, null, null, out targetOutputs);
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                Assert.True(targetOutputs.ContainsKey("Build"));
                Assert.Equal(5, targetOutputs["Build"].Items.Length);
                Assert.Equal("|a", targetOutputs["Build"].Items[0].ItemSpec);
                Assert.Equal("A|b", targetOutputs["Build"].Items[1].ItemSpec);
                Assert.Equal("B|c", targetOutputs["Build"].Items[2].ItemSpec);
                Assert.Equal("C|d", targetOutputs["Build"].Items[3].ItemSpec);
                Assert.Equal("D|", targetOutputs["Build"].Items[4].ItemSpec);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Orcas had a bug that if the target casing specified was not correct, we would still build it,
        /// but not return any target outputs!
        /// </summary>
        [Fact]
        public void TargetNameIsCaseInsensitive()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`Build` xmlns=`msbuildnamespace` ToolsVersion='msbuilddefaulttoolsversion'>
                    <Target Name=`bUiLd` Outputs=`foo.out` />
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project DefaultTargets=`t` xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>                  
                    <Target Name=`t`>
                        <MSBuild Projects=`" + projectFile1 + @"` Targets=`BUILD`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`out`/>
                        </MSBuild>
                        <Message Text=`[@(out)]`/>
                    </Target>	
                </Project>
                ");

            try
            {
                Project project = new Project(projectFile2);
                MockLogger logger = new MockLogger();

                Assert.True(project.Build(logger));

                logger.AssertLogContains("[foo.out]");
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        [Fact]
        public void ProjectFileWithoutNamespaceBuilds()
        {
            string projectFile1 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project>
                    <Target Name=`Build` Outputs=`foo.out` />
                </Project>
                ");

            string projectFile2 = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project>                  
                    <Target Name=`t`>
                        <MSBuild Projects=`" + projectFile1 + @"` Targets=`Build`>
                            <Output TaskParameter=`TargetOutputs` ItemName=`out`/>
                        </MSBuild>
                        <Message Text=`[@(out)]`/>
                    </Target>	
                </Project>
                ");

            try
            {
                Project project = new Project(projectFile2);
                MockLogger logger = new MockLogger();

                Assert.True(project.Build(logger));

                logger.AssertLogContains("[foo.out]");
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }
    }
}
