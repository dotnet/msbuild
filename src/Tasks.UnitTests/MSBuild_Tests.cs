// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class MSBuildTask_Tests : IDisposable
    {
        public MSBuildTask_Tests()
        {
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
                try
                {
                    MSBuild msbuildTask = new MSBuild();
                    msbuildTask.BuildEngine = new MockEngine();

                    msbuildTask.Projects = new ITaskItem[] { new TaskItem(projectFile1) };

                    bool success = msbuildTask.Execute();
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

            try
            {
                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();

                msbuildTask.Projects = new ITaskItem[] { new TaskItem(projectFile1), new TaskItem(projectFile2) };

                bool success = msbuildTask.Execute();
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

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        /// <summary>
        /// Ensures that it is possible to call the MSBuild task
        /// with an empty Projects parameter, and it shouldn't error, and it shouldn't try to
        /// build itself.
        /// </summary>
        [Fact]
        public void EmptyProjectsParameterResultsInNoop()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
	                    <MSBuild Projects=` @(empty) ` />
                    </Target>
                </Project>
                ");

            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build();
            Assert.True(success); // "Build failed.  See Standard Out tab for details"
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

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"SkipNonexistentProjectsMain.csproj");
            Assert.True(logger.FullLog.Contains("MSB3202")); // project file not found
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

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"SkipNonexistentProjectsMain.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(1, logger.ErrorCount);
            Assert.True(logger.FullLog.Contains("MSB3202")); // project file not found
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

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"SkipNonexistentProjectsMain.csproj");

            logger.AssertLogContains("Hello from foo.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(0, logger.ErrorCount);
            Assert.True(logger.FullLog.Contains("this_project_does_not_exist.csproj")); // for the missing project
            Assert.False(logger.FullLog.Contains("MSB3202")); // project file not found error
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

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"SkipNonexistentProjectsMain.csproj");

            logger.AssertLogContains("Hello from foo.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(0, logger.ErrorCount);
            Assert.True(logger.FullLog.Contains("this_project_does_not_exist.csproj")); // for the missing project
            Assert.False(logger.FullLog.Contains("MSB3202")); // project file not found error
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

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"BuildingVCProjMain.csproj");

            logger.AssertLogContains("Hello from foo.csproj");
            Assert.Equal(0, logger.WarningCount);
            Assert.Equal(1, logger.ErrorCount);
            Assert.True(logger.FullLog.Contains("MSB3204")); // upgrade to vcxproj needed
        }

        /// <summary>
        /// Calling the MSBuild task, passing in a property
        /// in the Properties parameter that has a special character in its value, such as semicolon.
        /// However, it's a situation where the project author doesn't have control over the
        /// property value and so he can't escape it himself.
        /// </summary>
#if RUNTIME_TYPE_NETCORE
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/259")]
#else
        [Fact]
#endif
        [Trait("Category", "mono-osx-failing")]
        public void PropertyOverridesContainSemicolon()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            // -------------------------------------------------------
            // ConsoleApplication1.csproj
            // -------------------------------------------------------

            // Just a normal console application project.
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                Path.Combine("bug'533'369", "Sub;Dir", "ConsoleApplication1", "ConsoleApplication1.csproj"), @"

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
                Path.Combine("bug'533'369", "Sub;Dir", "ConsoleApplication1", "Program.cs"), @"
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
                Path.Combine("bug'533'369", "Sub;Dir", "TeamBuild.proj"), @"
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

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess(Path.Combine("bug'533'369", "Sub;Dir", "TeamBuild.proj"));

            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory(Path.Combine("bug'533'369", "Sub;Dir", "binaries", "ConsoleApplication1.exe"));
        }

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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2),
                    new TaskItem(projectFile2)
                };
                projects[1].SetMetadata("Properties", "MyProp=1");
                projects[3].SetMetadata("Properties", "MyProp=1");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Projects = projects;
                msbuildTask.Properties = new string[] { "MyProp=0" };

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    a1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetA
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2),
                    new TaskItem(projectFile2)
                };
                projects[1].SetMetadata("Properties", "MyProp=1");
                projects[3].SetMetadata("Properties", "MyProp=1");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();

                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2),
                    new TaskItem(projectFile2)
                };
                projects[1].SetMetadata("Properties", "");
                projects[3].SetMetadata("Properties", "MyProp=1");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();

                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    h1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetH
                    ", projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2),
                    new TaskItem(projectFile2)
                };
                projects[1].SetMetadata("Properties", "=1");
                projects[3].SetMetadata("Properties", "=;1");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();

                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2)
                };
                projects[0].SetMetadata("AdditionalProperties", "MyPropA=1");
                projects[1].SetMetadata("AdditionalProperties", "MyPropA=0");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Properties = new string[] { "MyPropG=1" };
                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    a1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetA
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    ", projectFile1, projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2)
                };

                projects[0].SetMetadata("AdditionalProperties", "MyPropA=1");
                projects[1].SetMetadata("AdditionalProperties", "MyPropA=1");

                projects[0].SetMetadata("Properties", "MyPropG=1");
                projects[1].SetMetadata("Properties", "MyPropG=0");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    g1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetG
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile1),
                    new TaskItem(projectFile2)
                };

                projects[0].SetMetadata("AdditionalProperties", "MyPropA=1");
                projects[1].SetMetadata("AdditionalProperties", "MyPropA=1");

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                string expectedItemOutputs = string.Format(@"
                    b1.dll : MSBuildSourceProjectFile={0} ; MSBuildSourceTargetName=TargetB
                    h1.dll : MSBuildSourceProjectFile={1} ; MSBuildSourceTargetName=TargetH
                    ", projectFile1, projectFile2);

                ObjectModelHelpers.AssertItemsMatch(expectedItemOutputs, msbuildTask.TargetOutputs, false /* order of items not enforced */);
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile2)
                };

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
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
                    MSBuild msbuildTask = new MSBuild();
                    // By default IsMultipleNodesIs false
                    MockEngine mockEngine = new MockEngine();
                    mockEngine.IsRunningMultipleNodes = false;
                    msbuildTask.BuildEngine = mockEngine;
                    msbuildTask.Projects = projects;
                    msbuildTask.Targets = new string[] { "msbuild" };
                    // Make success true as the expected result is false
                    bool success = true;
                    switch (i)
                    {
                        case 0:
                            // Verify setting BuildInParallel and StopOnFirstFailure to
                            // true will cause the msbuild task to set BuildInParallel to false during the execute
                            msbuildTask.BuildInParallel = true;
                            msbuildTask.StopOnFirstFailure = true;
                            success = msbuildTask.Execute();
                            // Verify build did not build second project which has the message SecondProject
                            mockEngine.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.False(msbuildTask.BuildInParallel); // "Iteration of 0 Expected BuildInParallel to be false"
                            break;
                        case 1:
                            // Verify setting BuildInParallel to true and StopOnFirstFailure to
                            // false will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = true;
                            msbuildTask.StopOnFirstFailure = false;
                            success = msbuildTask.Execute();
                            // Verify build did  build second project which has the message SecondProject
                            mockEngine.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.True(msbuildTask.BuildInParallel); // "Iteration of 1 Expected BuildInParallel to be true"
                            break;
                        case 2:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to
                            // true will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = false;
                            msbuildTask.StopOnFirstFailure = true;
                            success = msbuildTask.Execute();
                            // Verify build did not build second project which has the message SecondProject
                            mockEngine.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.False(msbuildTask.BuildInParallel); // "Iteration of 2 Expected BuildInParallel to be false"
                            break;

                        case 3:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to
                            // false will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = false;
                            msbuildTask.StopOnFirstFailure = false;
                            success = msbuildTask.Execute();
                            // Verify build did build second project which has the message SecondProject
                            mockEngine.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.False(msbuildTask.BuildInParallel); // "Iteration of 3 Expected BuildInParallel to be false"
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
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(project1), new TaskItem(project2)
                };

                // Test the various combinations of BuildInParallel and StopOnFirstFailure when the msbuild task is told there are multiple nodes
                // running in the system
                for (int i = 0; i < 4; i++)
                {
                    MSBuild msbuildTask = new MSBuild();
                    MockEngine mockEngine = new MockEngine();
                    mockEngine.IsRunningMultipleNodes = true;
                    msbuildTask.BuildEngine = mockEngine;
                    msbuildTask.Projects = projects;
                    msbuildTask.Targets = new string[] { "msbuild" };
                    // Make success true as the expected result is false
                    bool success = true;
                    switch (i)
                    {
                        case 0:
                            // Verify setting BuildInParallel and StopOnFirstFailure to
                            // true will not cause the msbuild task to set BuildInParallel to false during the execute
                            msbuildTask.BuildInParallel = true;
                            msbuildTask.StopOnFirstFailure = true;
                            success = msbuildTask.Execute();
                            // Verify build did build second project which has the message SecondProject
                            mockEngine.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.True(msbuildTask.BuildInParallel); // "Iteration of 0 Expected BuildInParallel to be true"
                            break;
                        case 1:
                            // Verify setting BuildInParallel to true and StopOnFirstFailure to
                            // false will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = true;
                            msbuildTask.StopOnFirstFailure = false;
                            success = msbuildTask.Execute();
                            // Verify build did build second project which has the message SecondProject
                            mockEngine.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.True(msbuildTask.BuildInParallel); // "Iteration of 1 Expected BuildInParallel to be true"
                            break;
                        case 2:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to
                            // true will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = false;
                            msbuildTask.StopOnFirstFailure = true;
                            success = msbuildTask.Execute();
                            // Verify build did not build second project which has the message SecondProject
                            mockEngine.AssertLogDoesntContain("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.False(msbuildTask.BuildInParallel); // "Iteration of 2 Expected BuildInParallel to be false"
                            break;

                        case 3:
                            // Verify setting BuildInParallel to false and StopOnFirstFailure to
                            // false will cause no change in BuildInParallel
                            msbuildTask.BuildInParallel = false;
                            msbuildTask.StopOnFirstFailure = false;
                            success = msbuildTask.Execute();
                            // Verify build did build second project which has the message SecondProject
                            mockEngine.AssertLogContains("SecondProject");
                            // Verify the correct msbuild task messages are in the log
                            Assert.False(msbuildTask.BuildInParallel); // "Iteration of 3 Expected BuildInParallel to be false"
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
                // Test the case where there is only one project and it has an error
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(project1)
                };

                MSBuild msbuildTask = new MSBuild();
                MockEngine mockEngine = new MockEngine();
                mockEngine.IsRunningMultipleNodes = true;
                msbuildTask.BuildEngine = mockEngine;
                msbuildTask.Projects = projects;
                msbuildTask.Targets = new string[] { "msbuild" };
                msbuildTask.BuildInParallel = false;
                msbuildTask.StopOnFirstFailure = true;
                bool success = msbuildTask.Execute();
                Assert.False(success); // "Build Succeeded.  See 'Standard Out' tab for details."

                // Test the case where there are two projects and the last one has an error
                projects = new ITaskItem[]
                {
                    new TaskItem(project2), new TaskItem(project1)
                };

                msbuildTask = new MSBuild();
                mockEngine = new MockEngine();
                mockEngine.IsRunningMultipleNodes = true;
                msbuildTask.BuildEngine = mockEngine;
                msbuildTask.Projects = projects;
                msbuildTask.Targets = new string[] { "msbuild" };
                msbuildTask.BuildInParallel = false;
                msbuildTask.StopOnFirstFailure = true;
                success = msbuildTask.Execute();
                Assert.False(success); // "Build Succeeded.  See 'Standard Out' tab for details."
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
                    // Test the case where the error is in the last target
                    MSBuild msbuildTask = new MSBuild();
                    MockEngine mockEngine = new MockEngine();
                    msbuildTask.BuildEngine = mockEngine;
                    msbuildTask.Projects = projects;
                    // Set to true as the expected result is false
                    bool success = true;
                    switch (i)
                    {
                        case 0:
                            // Test the case where the error is in the last project and RunEachTargetSeparately = true
                            msbuildTask.StopOnFirstFailure = true;
                            msbuildTask.RunEachTargetSeparately = true;
                            msbuildTask.Targets = new string[] { "T1", "T2", "T3" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogContains("Proj2 T2 message");
                            break;
                        case 1:
                            // Test the case where the error is in the second target out of 3.
                            msbuildTask.StopOnFirstFailure = true;
                            msbuildTask.RunEachTargetSeparately = true;
                            msbuildTask.Targets = new string[] { "T1", "T3", "T2" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogDoesntContain("Proj2 T2 message");
                            // The build should fail as the first project has an error
                            break;
                        case 2:
                            // Test case where error is in second last target but stopOnFirstFailure is false
                            msbuildTask.RunEachTargetSeparately = true;
                            msbuildTask.StopOnFirstFailure = false;
                            msbuildTask.Targets = new string[] { "T1", "T3", "T2" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogContains("Proj2 T2 message");
                            break;
                        // Test the cases where RunEachTargetSeparately is false. In these cases all of the targets should be submitted at once
                        case 3:
                            // Test the case where the error is in the last project and RunEachTargetSeparately = true
                            msbuildTask.StopOnFirstFailure = true;
                            msbuildTask.Targets = new string[] { "T1", "T2", "T3" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogContains("Proj2 T2 message");
                            // The build should fail as the first project has an error
                            break;
                        case 4:
                            // Test the case where the error is in the second target out of 3.
                            msbuildTask.StopOnFirstFailure = true;
                            msbuildTask.Targets = new string[] { "T1", "T3", "T2" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogDoesntContain("Proj2 T2 message");
                            // The build should fail as the first project has an error
                            break;
                        case 5:
                            // Test case where error is in second last target but stopOnFirstFailure is false
                            msbuildTask.StopOnFirstFailure = false;
                            msbuildTask.Targets = new string[] { "T1", "T3", "T2" };
                            success = msbuildTask.Execute();
                            mockEngine.AssertLogContains("Proj2 T1 message");
                            mockEngine.AssertLogDoesntContain("Proj2 T2 message");
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

            try
            {
                ITaskItem[] projects = new ITaskItem[]
                {
                    new TaskItem(projectFile2)
                };

                MSBuild msbuildTask = new MSBuild();
                msbuildTask.BuildEngine = new MockEngine();
                msbuildTask.Projects = projects;

                bool success = msbuildTask.Execute();
                Assert.True(success); // "Build failed.  See 'Standard Out' tab for details."

                Assert.Equal(5, msbuildTask.TargetOutputs.Length);
                Assert.Equal("|a", msbuildTask.TargetOutputs[0].ItemSpec);
                Assert.Equal("A|b", msbuildTask.TargetOutputs[1].ItemSpec);
                Assert.Equal("B|c", msbuildTask.TargetOutputs[2].ItemSpec);
                Assert.Equal("C|d", msbuildTask.TargetOutputs[3].ItemSpec);
                Assert.Equal("D|", msbuildTask.TargetOutputs[4].ItemSpec);
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

                project.Build(logger);

                logger.AssertLogContains("[foo.out]");
            }
            finally
            {
                File.Delete(projectFile1);
                File.Delete(projectFile2);
            }
        }

        [Fact]
        public void NonexistentTargetSkippedWhenSkipNonexistentTargetsSet()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentTargets.csproj",
                @"<Project>
                    <Target Name=`t` >
	                    <MSBuild Projects=`SkipNonexistentTargets.csproj` Targets=`t_nonexistent;t2` SkipNonexistentTargets=`true` />
                     </Target>
                    <Target Name=`t2`> <Message Text=`t2 executing` /> </Target>
                </Project>
                ");

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess(@"SkipNonexistentTargets.csproj");

            // Target t2 should still execute
            logger.FullLog.ShouldContain("t2 executing");
            logger.WarningCount.ShouldBe(0);
            logger.ErrorCount.ShouldBe(0);

            // t_missing target should be skipped
            logger.FullLog.ShouldContain("Target \"t_nonexistent\" skipped");
        }

        [Fact]
        public void NonexistentTargetFailsAfterSkipped()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory(
                "SkipNonexistentTargets.csproj",
                @"<Project>
                    <Target Name=`t` >
	                    <MSBuild Projects=`SkipNonexistentTargets.csproj` Targets=`t_nonexistent` SkipNonexistentTargets=`true` />
                        <MSBuild Projects=`SkipNonexistentTargets.csproj` Targets=`t_nonexistent` SkipNonexistentTargets=`false` />
                     </Target>
                </Project>
                ");

            MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectFailure(@"SkipNonexistentTargets.csproj");

            logger.WarningCount.ShouldBe(0);
            logger.ErrorCount.ShouldBe(1);

            // t_missing target should be skipped
            logger.FullLog.ShouldContain("Target \"t_nonexistent\" skipped");

            // 2nd invocation of t_missing should fail the build resulting in target not found error (MSB4057)
            logger.FullLog.ShouldContain("MSB4057");
        }
    }
}
