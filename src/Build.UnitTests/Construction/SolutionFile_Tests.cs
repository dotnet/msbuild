// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Shared;
using Shouldly;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    public class SolutionFile_Tests
    {
        /// <summary>
        /// Test just the most basic, plain vanilla first project line.
        /// </summary>
        [Fact]
        public void BasicParseFirstProjectLine()
        {
            SolutionFile p = new SolutionFile();
            p.FullPath = NativeMethodsShared.IsWindows ? "c:\\foo.sln" : "/foo.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{Project GUID}\") = \"Project name\", \"Relative path to project file\", \"Unique name-GUID\"",
                 proj
            );
            Assert.Equal(SolutionProjectType.Unknown, proj.ProjectType);
            Assert.Equal("Project name", proj.ProjectName);
            Assert.Equal("Relative path to project file", proj.RelativePath);
            Assert.Equal("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// Test that the first project line of a project with the C++ project guid and an
        /// extension of vcproj is seen as invalid.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ParseFirstProjectLine_VC()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                SolutionFile p = new SolutionFile();
                p.FullPath = "c:\\foo.sln";
                ProjectInSolution proj = new ProjectInSolution(p);

                p.ParseFirstProjectLine
                (
                    "Project(\"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}\") = \"Project name.vcproj\", \"Relative path\\to\\Project name.vcproj\", \"Unique name-GUID\"",
                     proj
                );

                Assert.True(false, "Should not get here");
            }
           );
        }
        /// <summary>
        /// Test that the first project line of a project with the C++ project guid and an
        /// arbitrary extension is seen as valid -- we assume that all C++ projects except
        /// .vcproj are MSBuild format.
        /// </summary>
        [Fact]
        public void ParseFirstProjectLine_VC2()
        {
            SolutionFile p = new SolutionFile();
            p.FullPath = NativeMethodsShared.IsWindows ? "c:\\foo.sln" : "/foo.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}\") = \"Project name.myvctype\", \"Relative path\\to\\Project name.myvctype\", \"Unique name-GUID\"",
                 proj
            );
            Assert.Equal(SolutionProjectType.KnownToBeMSBuildFormat, proj.ProjectType);
            Assert.Equal("Project name.myvctype", proj.ProjectName);
            Assert.Equal("Relative path\\to\\Project name.myvctype", proj.RelativePath);
            Assert.Equal("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// A slightly more complicated test where there is some different whitespace.
        /// </summary>
        [Fact]
        public void ParseFirstProjectLineWithDifferentSpacing()
        {
            SolutionFile p = new SolutionFile();
            p.FullPath = NativeMethodsShared.IsWindows ? "c:\\foo.sln" : "/foo.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\" {Project GUID} \")  = \" Project name \",  \" Relative path to project file \"    , \" Unique name-GUID \"",
                 proj
            );
            Assert.Equal(SolutionProjectType.Unknown, proj.ProjectType);
            Assert.Equal("Project name", proj.ProjectName);
            Assert.Equal("Relative path to project file", proj.RelativePath);
            Assert.Equal("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// First project line with an empty project name.  This is somewhat malformed, but we should
        /// still behave reasonably instead of crashing.
        /// </summary>
        [Fact]
        public void ParseFirstProjectLine_InvalidProject()
        {
            SolutionFile p = new SolutionFile();
            p.FullPath = NativeMethodsShared.IsWindows ? "c:\\foo.sln" : "/foo.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{Project GUID}\") = \"\", \"src\\.proj\", \"Unique name-GUID\"",
                 proj
            );
            Assert.Equal(SolutionProjectType.Unknown, proj.ProjectType);
            Assert.StartsWith("EmptyProjectName", proj.ProjectName);
            Assert.Equal("src\\.proj", proj.RelativePath);
            Assert.Equal("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        [Fact]
        public void ParseEtpProject()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            try
            {
                // Create the first .etp project file
                string etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <Views>
                            <ProjectExplorer>
                                <File>ClassLibrary2.csproj</File>
                            </ProjectExplorer>
                        </Views>
                        <References>
                            <Reference>
                                <FILE>ClassLibrary2.csproj</FILE>
                                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                File.WriteAllText(proj1Path, etpProjContent);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";
                SolutionFile solution = ParseSolutionHelper(solutionFileContents);
                //Project should get added to the solution
                Assert.Equal(@"someproj.etp", solution.ProjectsInOrder[0].RelativePath);
                Assert.Equal(@"ClassLibrary2.csproj", solution.ProjectsInOrder[1].RelativePath);
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
            }
        }

        /// <summary>
        /// Test CanBeMSBuildFile
        /// </summary>
        [Fact]
        public void CanBeMSBuildFile()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            string proj2Path = Path.Combine(Path.GetTempPath(), "someproja.proj");
            try
            {
                // Create the first .etp project file
                string etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <Views>
                            <ProjectExplorer>
                                <File>ClassLibrary2.csproj</File>
                            </ProjectExplorer>
                        </Views>
                        <References>
                            <Reference>
                                <FILE>ClassLibrary2.csproj</FILE>
                                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                string genericProj = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"" xmlns=""msbuildnamespace"">
                    <ItemGroup>
                        <Reference Include=""System"" />
                        <Reference Include=""System.Data"" />
                        <Reference Include=""System.Xml"" />
                    </ItemGroup>
                    <ItemGroup>
                        <Compile Include=""Class1.cs"" />
                        <Compile Include=""Properties\AssemblyInfo.cs"" />
                    </ItemGroup>
                    <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
                </Project>
                ");

                File.WriteAllText(proj1Path, etpProjContent);
                File.WriteAllText(proj2Path, genericProj);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject
                        Project('{NNNNNNNN-9925-4D57-9DAF-E0A9D936ABDB}') = 'someproja', 'someproja.proj', '{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";


                SolutionFile solution = ParseSolutionHelper(solutionFileContents);
                ProjectInSolution project = (ProjectInSolution)solution.ProjectsByGuid["{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}"];
                ProjectInSolution project2 = (ProjectInSolution)solution.ProjectsByGuid["{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}"];
                string error = null;
                Assert.False(project.CanBeMSBuildProjectFile(out error));
                Assert.True(project2.CanBeMSBuildProjectFile(out error));
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
                File.Delete(proj2Path);
            }
        }

        /// <summary>
        /// Test CanBeMSBuildFile
        /// </summary>
        [Fact]
        public void CanBeMSBuildFileRejectsMSBuildLikeFiles()
        {
            using (var env = TestEnvironment.Create())
            {
                string rptprojProjContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" ToolsVersion=""2.0"">
                      <DataSources />
                      <Reports />
                    </Project>";
                string dwprojProjContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Project xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:ddl2=""http://schemas.microsoft.com/analysisservices/2003/engine/2"" xmlns:ddl2_2=""http://schemas.microsoft.com/analysisservices/2003/engine/2/2"" xmlns:ddl100_100=""http://schemas.microsoft.com/analysisservices/2008/engine/100/100"" xmlns:ddl200=""http://schemas.microsoft.com/analysisservices/2010/engine/200"" xmlns:ddl200_200=""http://schemas.microsoft.com/analysisservices/2010/engine/200/200"" xmlns:dwd=""http://schemas.microsoft.com/DataWarehouse/Designer/1.0"">
                      <ProductVersion />
                      <SchemaVersion />
                      <State />
                      <Database />
                      <Cubes />
                    </Project>";

                string rptprojPath = env.CreateFile(".rptproj").Path;
                File.WriteAllText(rptprojPath, rptprojProjContent);
                string dqprojPath = env.CreateFile(".dwproj").Path;
                File.WriteAllText(dqprojPath, dwprojProjContent);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{F14B399A-7131-4C87-9E4B-1186C45EF12D}') = 'PrtProj', '" + Path.GetFileName(rptprojPath) + @"', '{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject
                        Project('{D2ABAB84-BF74-430A-B69E-9DC6D40DDA17}') = 'DwProj', '" + Path.GetFileName(dqprojPath) + @"', '{DEA89696-F42B-4B58-B7EE-017FF40817D1}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

                string error = null;
                SolutionFile solution = ParseSolutionHelper(solutionFileContents);
                ProjectInSolution project1 = solution.ProjectsByGuid["{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}"];
                ProjectInSolution project2 = solution.ProjectsByGuid["{DEA89696-F42B-4B58-B7EE-017FF40817D1}"];

                project1.CanBeMSBuildProjectFile(out error).ShouldBe(false);
                project2.CanBeMSBuildProjectFile(out error).ShouldBe(false);
            }
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        [Fact]
        public void ParseNestedEtpProjectSingleLevel()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            string proj2Path = Path.Combine(Path.GetTempPath(), "someproj2.etp");
            try
            {
                // Create the first .etp project file
                string etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>someproj2.etp</FILE>
                                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                File.WriteAllText(proj1Path, etpProjContent);

                // Create the second .etp project file
                etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>ClassLibrary1.csproj</FILE>
                                <GUIDPROJECTID>{83D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FF}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                File.WriteAllText(proj2Path, etpProjContent);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";
                SolutionFile solution = ParseSolutionHelper(solutionFileContents);

                //Project should get added to the solution
                Assert.Equal(@"someproj.etp", solution.ProjectsInOrder[0].RelativePath);
                Assert.Equal(@"someproj2.etp", solution.ProjectsInOrder[1].RelativePath);
                Assert.Equal(@"ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
                File.Delete(proj2Path);
            }
        }

        [Fact]
        public void TestVSAndSolutionVersionParsing()
        {
            // Create the SolutionFile object
            string solutionFileContentsPriorToDev12 =
                @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionPriorToDev12 = ParseSolutionHelper(solutionFileContentsPriorToDev12);

            Assert.Equal(11, solutionPriorToDev12.Version);
            Assert.Equal(10, solutionPriorToDev12.VisualStudioVersion);

            // Create the SolutionFile object
            string solutionFileContentsDev12 =
                @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion = 12.0.20311.0 VSPRO_PLATFORM
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12 = ParseSolutionHelper(solutionFileContentsDev12);

            Assert.Equal(11, solutionDev12.Version);
            Assert.Equal(12, solutionDev12.VisualStudioVersion);

            // Test parsing of corrupted VisualStudioVersion lines

            // Version number deleted
            string solutionFileContentsDev12Corrupted1 =
                @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion = VSPRO_PLATFORM
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted1 = ParseSolutionHelper(solutionFileContentsDev12Corrupted1);
            Assert.Equal(11, solutionDev12Corrupted1.Version);
            Assert.Equal(10, solutionDev12Corrupted1.VisualStudioVersion);

            // Remove version number and VSPRO_PLATFORM tag
            string solutionFileContentsDev12Corrupted2 =
               @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion = 
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted2 = ParseSolutionHelper(solutionFileContentsDev12Corrupted2);
            Assert.Equal(11, solutionDev12Corrupted2.Version);
            Assert.Equal(10, solutionDev12Corrupted2.VisualStudioVersion);

            // Switch positions between VSPRO_PLATFORM tag and version number
            string solutionFileContentsDev12Corrupted3 =
               @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion = VSPRO_PLATFORM 12.0.20311.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted3 = ParseSolutionHelper(solutionFileContentsDev12Corrupted3);
            Assert.Equal(11, solutionDev12Corrupted3.Version);
            Assert.Equal(10, solutionDev12Corrupted3.VisualStudioVersion);

            // Add a number of spaces before version number and glue it together with VSPRO_PLATFORM
            string solutionFileContentsDev12Corrupted4 =
               @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion =                   12.0.20311.0VSPRO_PLATFORM
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted4 = ParseSolutionHelper(solutionFileContentsDev12Corrupted4);
            Assert.Equal(11, solutionDev12Corrupted4.Version);
            Assert.Equal(10, solutionDev12Corrupted4.VisualStudioVersion);

            // Corrupted version number
            string solutionFileContentsDev12Corrupted5 =
               @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion = ...12..0,.20311.0 VSPRO_PLATFORM
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted5 = ParseSolutionHelper(solutionFileContentsDev12Corrupted5);
            Assert.Equal(11, solutionDev12Corrupted5.Version);
            Assert.Equal(10, solutionDev12Corrupted5.VisualStudioVersion);

            // Add a number of spaces before version number
            string solutionFileContentsDev12Corrupted6 =
               @"
                    Microsoft Visual Studio Solution File, Format Version 11.00
                        VisualStudioVersion =                   12.0.20311.0 VSPRO_PLATFORM
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";

            SolutionFile solutionDev12Corrupted6 = ParseSolutionHelper(solutionFileContentsDev12Corrupted6);
            Assert.Equal(11, solutionDev12Corrupted6.Version);
            Assert.Equal(12, solutionDev12Corrupted6.VisualStudioVersion);
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ParseNestedEtpProjectMultipleLevel()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            string proj2Path = Path.Combine(Path.GetTempPath(), "someproj2.etp");
            string proj3Path = Path.Combine(Path.GetTempPath(), "ETPProjUpgradeTest", "someproj3.etp");
            try
            {
                // Create the first .etp project file
                string etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>someproj2.etp</FILE>
                                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                File.WriteAllText(proj1Path, etpProjContent);

                // Create the second .etp project file
                etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>ETPProjUpgradeTest\someproj3.etp</FILE>
                                <GUIDPROJECTID>{83D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FF}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";

                File.WriteAllText(proj2Path, etpProjContent);

                // Create the third .etp project file
                etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>" + Path.Combine("..", "SomeFolder", "ClassLibrary1.csproj") + @"</FILE>
                                <GUIDPROJECTID>{83D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FF}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";
                //Create the directory for the third project
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ETPProjUpgradeTest"));
                File.WriteAllText(proj3Path, etpProjContent);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";
                SolutionFile solution = ParseSolutionHelper(solutionFileContents);

                //Project should get added to the solution
                Assert.Equal(@"someproj.etp", solution.ProjectsInOrder[0].RelativePath);
                Assert.Equal(@"someproj2.etp", solution.ProjectsInOrder[1].RelativePath);
                Assert.Equal(@"ETPProjUpgradeTest\someproj3.etp", solution.ProjectsInOrder[2].RelativePath);
                Assert.Equal(
                    Path.Combine("ETPProjUpgradeTest", "..", "SomeFolder", "ClassLibrary1.csproj"),
                    solution.ProjectsInOrder[3].RelativePath);
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
                File.Delete(proj2Path);
                File.Delete(proj3Path);
            }
        }

        /// <summary>
        /// Ensure that a malformed .etp proj file listed in the .SLN file results in an
        /// InvalidProjectFileException.
        /// </summary>
        [Fact]
        public void MalformedEtpProjFile()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            try
            {
                // Create the .etp project file
                // Note the </EFPROJECT> is missing
                string etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <Views>
                            <ProjectExplorer>
                                <File>ClassLibrary2\ClassLibrary2.csproj</File>
                            </ProjectExplorer>
                        </Views>
                        <References>
                            <Reference>
                                <FILE>ClassLibrary2\ClassLibrary2.csproj</FILE>
                                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                ";

                File.WriteAllText(proj1Path, etpProjContent);

                // Create the SolutionFile object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                            ProjectSection(ProjectDependencies) = postProject
                            EndProjectSection
                        EndProject";
                SolutionFile solution = ParseSolutionHelper(solutionFileContents);
                string errCode, ignoredKeyword;
                ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errCode, out ignoredKeyword, "Shared.InvalidProjectFile",
                   "someproj.etp", String.Empty);
                foreach (string warningString in solution.SolutionParserWarnings)
                {
                    Console.WriteLine(warningString.ToString());
                }
                Assert.Contains(errCode, solution.SolutionParserErrorCodes[0].ToString());
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
            }
        }

        /// <summary>
        /// Ensure that a missing .etp proj file listed in the .SLN file results in an
        /// InvalidProjectFileException.
        /// </summary>
        [Fact]
        public void MissingEtpProjFile()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            // Create the solution file
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 8.00
                    Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                        ProjectSection(ProjectDependencies) = postProject
                        EndProjectSection
                    EndProject";
            // Delete the someproj.etp file if it exists
            File.Delete(proj1Path);
            SolutionFile solution = ParseSolutionHelper(solutionFileContents);
            string errCode, ignoredKeyword;
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                  "someproj.etp", String.Empty);
            Assert.Contains(errCode, solution.SolutionParserErrorCodes[0].ToString());
        }

        /// <summary>
        /// Test some characters that are valid in a file name but that also could be
        /// considered a delimiter by a parser. Does quoting work for special characters?
        /// </summary>
        [Fact]
        public void ParseFirstProjectLineWhereProjectNameHasSpecialCharacters()
        {
            SolutionFile p = new SolutionFile();
            p.FullPath = NativeMethodsShared.IsWindows ? "c:\\foo.sln" : "/foo.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{Project GUID}\")  = \"MyProject,(=IsGreat)\",  \"Relative path to project file\"    , \"Unique name-GUID\"",
                 proj
            );
            Assert.Equal(SolutionProjectType.Unknown, proj.ProjectType);
            Assert.Equal("MyProject,(=IsGreat)", proj.ProjectName);
            Assert.Equal("Relative path to project file", proj.RelativePath);
            Assert.Equal("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// Test some characters that are valid in a file name but that also could be
        /// considered a delimiter by a parser. Does quoting work for special characters?
        /// </summary>
        [Fact]
        public void ParseFirstProjectLineWhereProjectPathHasBackslash()
        {
            using (var env = TestEnvironment.Create())
            {
                var solutionFolder = env.CreateFolder(Path.Combine(FileUtilities.GetTemporaryDirectory(), "sln"));
                env.CreateFolder(Path.Combine(solutionFolder.Path, "RelativePath"));

                SolutionFile p = new SolutionFile();
                p.FullPath = Path.Combine(solutionFolder.Path, "RelativePath", "project file");
                p.SolutionFileDirectory = Path.GetFullPath(solutionFolder.Path);
                ProjectInSolution proj = new ProjectInSolution(p);

                p.ParseFirstProjectLine
                (
                    "Project(\"{Project GUID}\")  = \"ProjectInSubdirectory\",  \"RelativePath\\project file\"    , \"Unique name-GUID\"",
                    proj
                );
                Assert.Equal(SolutionProjectType.Unknown, proj.ProjectType);
                Assert.Equal("ProjectInSubdirectory", proj.ProjectName);
                Assert.Equal(Path.Combine("RelativePath", "project file"), proj.RelativePath);
                Assert.Equal("Unique name-GUID", proj.ProjectGuid);
            }
        }

        /// <summary>
        /// Helper method to create a SolutionFile object, and call it to parse the SLN file
        /// represented by the string contents passed in.
        /// </summary>
        /// <param name="solutionFileContents"></param>
        /// <returns></returns>
        static internal SolutionFile ParseSolutionHelper(string solutionFileContents)
        {
            solutionFileContents = solutionFileContents.Replace('\'', '"');
            StreamReader sr = StreamHelpers.StringToStreamReader(solutionFileContents);

            SolutionFile sp = new SolutionFile();
            sp.SolutionFileDirectory = Path.GetTempPath();
            sp.SolutionReader = sr;
            sp.FullPath = FileUtilities.GetTemporaryFileName(".sln");
            sp.ParseSolution();
            // Clean up the temporary file that got created with this call
            return sp;
        }

        /// <summary>
        /// Ensure that a bogus version stamp in the .SLN file results in an
        /// InvalidProjectFileException.
        /// </summary>
        [Fact]
        public void BadVersionStamp()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version a.b
                # Visual Studio 2005
                ";

                ParseSolutionHelper(solutionFileContents);
            }
           );
        }
        /// <summary>
        /// Expected version numbers less than 7 to cause an invalid project file exception.
        /// </summary>
        [Fact]
        public void VersionTooLow()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version 6.0
                # Visual Studio 2005
                ";

                ParseSolutionHelper(solutionFileContents);
            }
           );
        }
        /// <summary>
        /// Ensure that an unsupported version greater than the current maximum (10) in the .SLN file results in a
        /// comment indicating we will try and continue
        /// </summary>
        [Fact]
        public void UnsupportedVersion()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 999.0
                # Visual Studio 2005
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);
            Assert.Single(solution.SolutionParserComments); // "Expected the solution parser to contain one comment"
            Assert.Equal(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedSolutionComment", "999"), (string)solution.SolutionParserComments[0]);
        }

        [Fact]
        public void Version9()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.0
                # Visual Studio 2005
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(9, solution.Version);
        }

        [Fact]
        public void Version10()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 10.0
                # Visual Studio 2005
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(10, solution.Version);
        }

        /// <summary>
        /// Test to parse a very basic .sln file to validate that description property in a solution file
        /// is properly handled.
        /// </summary>
        [Fact]
        public void ParseSolutionFileWithDescriptionInformation()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'AnyProject', 'AnyProject\AnyProject.csproj', '{2CAB0FBD-15D8-458B-8E63-1B5B840E9798}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                        Description = Some description of this solution
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {2CAB0FBD-15D8-458B-8E63-1B5B840E9798}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {2CAB0FBD-15D8-458B-8E63-1B5B840E9798}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {2CAB0FBD-15D8-458B-8E63-1B5B840E9798}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {2CAB0FBD-15D8-458B-8E63-1B5B840E9798}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";
            try
            {
                ParseSolutionHelper(solutionFileContents);
            }
            catch (Exception ex)
            {
                Assert.True(false, "Failed to parse solution containing description information. Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Tests the parsing of a very basic .SLN file with three independent projects.
        /// </summary>
        [Fact]
        public void BasicSolution()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{F184B08F-C81C-45F6-A57F-5ABD9991F28F}') = 'ConsoleApplication1', 'ConsoleApplication1\ConsoleApplication1.vbproj', '{AB3413A6-D689-486D-B7F0-A095371B3F13}'
                EndProject
                Project('{F184B08F-C81C-45F6-A57F-5ABD9991F28F}') = 'vbClassLibrary', 'vbClassLibrary\vbClassLibrary.vbproj', '{BA333A76-4511-47B8-8DF4-CA51C303AD0B}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{DEBCE986-61B9-435E-8018-44B9EF751655}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|AnyCPU = Debug|AnyCPU
                        Release|AnyCPU = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.Build.0 = Release|AnyCPU
                        {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Release|AnyCPU.Build.0 = Release|AnyCPU
                        {DEBCE986-61B9-435E-8018-44B9EF751655}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {DEBCE986-61B9-435E-8018-44B9EF751655}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {DEBCE986-61B9-435E-8018-44B9EF751655}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {DEBCE986-61B9-435E-8018-44B9EF751655}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            Assert.Equal(SolutionProjectType.KnownToBeMSBuildFormat, solution.ProjectsInOrder[0].ProjectType);
            Assert.Equal("ConsoleApplication1", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal(@"ConsoleApplication1\ConsoleApplication1.vbproj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{AB3413A6-D689-486D-B7F0-A095371B3F13}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[0].Dependencies);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);
            Assert.Equal("ConsoleApplication1", solution.ProjectsInOrder[0].GetUniqueProjectName());

            Assert.Equal(SolutionProjectType.KnownToBeMSBuildFormat, solution.ProjectsInOrder[1].ProjectType);
            Assert.Equal("vbClassLibrary", solution.ProjectsInOrder[1].ProjectName);
            Assert.Equal(@"vbClassLibrary\vbClassLibrary.vbproj", solution.ProjectsInOrder[1].RelativePath);
            Assert.Equal("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[1].Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);
            Assert.Equal("vbClassLibrary", solution.ProjectsInOrder[1].GetUniqueProjectName());

            Assert.Equal(SolutionProjectType.KnownToBeMSBuildFormat, solution.ProjectsInOrder[2].ProjectType);
            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[2].ProjectName);
            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{DEBCE986-61B9-435E-8018-44B9EF751655}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);
            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[2].GetUniqueProjectName());
        }

        /// <summary>
        /// Exercises solution folders, and makes sure that samely named projects in different
        /// solution folders will get correctly uniquified.
        /// </summary>
        [Fact]
        public void SolutionFolders()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{34E0D07D-CF8F-459D-9449-C4188D8C5564}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{E0F97730-25D2-418A-A7BD-02CAFDC6E470}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj', '{A5EE8128-B08E-4533-86C5-E46714981680}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySubSlnFolder', 'MySubSlnFolder', '{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.csproj', '{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.Build.0 = Release|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {A5EE8128-B08E-4533-86C5-E46714981680} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4} = {2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(5, solution.ProjectsInOrder.Count);

            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{34E0D07D-CF8F-459D-9449-C4188D8C5564}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[0].Dependencies);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);
            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[0].GetUniqueProjectName());

            Assert.Equal(SolutionProjectType.SolutionFolder, solution.ProjectsInOrder[1].ProjectType);
            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[1].Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);
            Assert.Equal("MySlnFolder", solution.ProjectsInOrder[1].GetUniqueProjectName());

            Assert.Equal(@"MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{A5EE8128-B08E-4533-86C5-E46714981680}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[2].ParentProjectGuid);
            Assert.Equal(@"MySlnFolder\ClassLibrary1", solution.ProjectsInOrder[2].GetUniqueProjectName());

            Assert.Equal(SolutionProjectType.SolutionFolder, solution.ProjectsInOrder[3].ProjectType);
            Assert.Equal("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", solution.ProjectsInOrder[3].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[3].Dependencies);
            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[3].ParentProjectGuid);
            Assert.Equal(@"MySlnFolder\MySubSlnFolder", solution.ProjectsInOrder[3].GetUniqueProjectName());

            Assert.Equal(@"ClassLibrary2\ClassLibrary2.csproj", solution.ProjectsInOrder[4].RelativePath);
            Assert.Equal("{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}", solution.ProjectsInOrder[4].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[4].Dependencies);
            Assert.Equal("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", solution.ProjectsInOrder[4].ParentProjectGuid);
            Assert.Equal(@"MySlnFolder\MySubSlnFolder\ClassLibrary2", solution.ProjectsInOrder[4].GetUniqueProjectName());
        }

        /// <summary>
        /// Parses solution configuration file that contains empty or whitespace lines
        /// to simulate a possible source control merge scenario.
        /// </summary>
        [Fact]
        public void ParseSolutionConfigurationWithEmptyLines()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{34E0D07D-CF8F-459D-9449-C4188D8C5564}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{E0F97730-25D2-418A-A7BD-02CAFDC6E470}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj', '{A5EE8128-B08E-4533-86C5-E46714981680}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySubSlnFolder', 'MySubSlnFolder', '{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.csproj', '{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution

                        Debug|Any CPU = Debug|Any CPU
                        
                        Release|Any CPU = Release|Any CPU
 
    
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution

                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.Build.0 = Release|Any CPU
 
  
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.Build.0 = Release|Any CPU
    
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        
                        {A5EE8128-B08E-4533-86C5-E46714981680} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4} = {2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}
                        

                    EndGlobalSection
                EndGlobal
                ";

            ParseSolutionHelper(solutionFileContents);
        }

        /// <summary>
        /// Tests situation where there's a nonexistent project listed in the solution folders.  We should
        /// error with a useful message.
        /// </summary>
        [Fact]
        public void MissingNestedProject()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{E0F97730-25D2-418A-A7BD-02CAFDC6E470}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj', '{A5EE8128-B08E-4533-86C5-E46714981680}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {34E0D07D-CF8F-459D-9449-C4188D8C5564}.Release|Any CPU.Build.0 = Release|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {A5EE8128-B08E-4533-86C5-E46714981680} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                    EndGlobalSection
                EndGlobal
                ";

            try
            {
                ParseSolutionHelper(solutionFileContents);
            }
            catch (InvalidProjectFileException e)
            {
                Assert.Equal("MSB5023", e.ErrorCode);
                Assert.Contains("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", e.Message);
                return;
            }

            // Should not get here
            Assert.True(false);
        }


        /// <summary>
        /// Verifies that we correctly identify solution folders and mercury non-buildable projects both as
        /// "non-building"
        /// </summary>
        [Fact]
        public void BuildableProjects()
        {
            string solutionFileContents =
                @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2013
VisualStudioVersion = 12.0.21119.0
MinimumVisualStudioVersion = 10.0.40219.1
Project('{D954291E-2A0B-460D-934E-DC6B0785DB48}') = 'HubApp2', 'HubApp2\HubApp2.scproj', '{892B5932-9AA8-46F9-A857-8967DCDBE4F5}'
EndProject
Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'HubApp2.Store', 'HubApp2\Store\HubApp2.Store.vcxproj', '{A5526AEA-E0A2-496D-94B7-2BBE835C83F8}'
EndProject
Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'Shared', 'HubApp2\Shared\Shared.vcxitems', '{FF6AEDF3-950A-46DD-910B-52BC69B9C99A}'
EndProject
Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'HubApp2.Phone', 'HubApp2\Phone\HubApp2.Phone.vcxproj', '{024E8607-06B0-440D-8741-5A888DC4B176}'
EndProject
Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{E0F97730-25D2-418A-A7BD-02CAFDC6E470}'
EndProject
Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{A5EE8128-B08E-4533-86C5-E46714981680}'
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Debug|ARM = Debug|ARM
        Debug|Mixed Platforms = Debug|Mixed Platforms
        Debug|Win32 = Debug|Win32
        Debug|x64 = Debug|x64
        Debug|x86 = Debug|x86
        Release|Any CPU = Release|Any CPU
        Release|ARM = Release|ARM
        Release|Mixed Platforms = Release|Mixed Platforms
        Release|Win32 = Release|Win32
        Release|x64 = Release|x64
        Release|x86 = Release|x86
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Any CPU.ActiveCfg = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|ARM.ActiveCfg = Debug|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|ARM.Build.0 = Debug|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|ARM.Deploy.0 = Debug|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Mixed Platforms.ActiveCfg = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Mixed Platforms.Build.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Mixed Platforms.Deploy.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Win32.ActiveCfg = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Win32.Build.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|Win32.Deploy.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x64.ActiveCfg = Debug|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x64.Build.0 = Debug|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x64.Deploy.0 = Debug|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x86.ActiveCfg = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x86.Build.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Debug|x86.Deploy.0 = Debug|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Any CPU.ActiveCfg = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|ARM.ActiveCfg = Release|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|ARM.Build.0 = Release|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|ARM.Deploy.0 = Release|ARM
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Mixed Platforms.ActiveCfg = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Mixed Platforms.Build.0 = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Mixed Platforms.Deploy.0 = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Win32.ActiveCfg = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Win32.Build.0 = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|Win32.Deploy.0 = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x64.ActiveCfg = Release|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x64.Build.0 = Release|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x64.Deploy.0 = Release|x64
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x86.ActiveCfg = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x86.Build.0 = Release|Win32
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8}.Release|x86.Deploy.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Any CPU.ActiveCfg = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|ARM.ActiveCfg = Debug|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|ARM.Build.0 = Debug|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|ARM.Deploy.0 = Debug|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Mixed Platforms.ActiveCfg = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Mixed Platforms.Build.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Mixed Platforms.Deploy.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Win32.ActiveCfg = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Win32.Build.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|Win32.Deploy.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|x64.ActiveCfg = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|x86.ActiveCfg = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|x86.Build.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Debug|x86.Deploy.0 = Debug|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Any CPU.ActiveCfg = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|ARM.ActiveCfg = Release|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|ARM.Build.0 = Release|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|ARM.Deploy.0 = Release|ARM
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Mixed Platforms.ActiveCfg = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Mixed Platforms.Build.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Mixed Platforms.Deploy.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Win32.ActiveCfg = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Win32.Build.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|Win32.Deploy.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|x64.ActiveCfg = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|x86.ActiveCfg = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|x86.Build.0 = Release|Win32
        {024E8607-06B0-440D-8741-5A888DC4B176}.Release|x86.Deploy.0 = Release|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|x86.ActiveCfg = Debug|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|x86.Build.0 = Debug|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Debug|x86.Deploy.0 = Debug|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|x86.ActiveCfg = Release|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|x86.Build.0 = Release|Win32
        {A5EE8128-B08E-4533-86C5-E46714981680}.Release|x86.Deploy.0 = Release|Win32
    EndGlobalSection
    GlobalSection(SolutionProperties) = preSolution
        HideSolutionNode = FALSE
    EndGlobalSection
    GlobalSection(NestedProjects) = preSolution
        {A5526AEA-E0A2-496D-94B7-2BBE835C83F8} = {892B5932-9AA8-46F9-A857-8967DCDBE4F5}
        {FF6AEDF3-950A-46DD-910B-52BC69B9C99A} = {892B5932-9AA8-46F9-A857-8967DCDBE4F5}
        {024E8607-06B0-440D-8741-5A888DC4B176} = {892B5932-9AA8-46F9-A857-8967DCDBE4F5}
    EndGlobalSection
EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(6, solution.ProjectsInOrder.Count);

            Assert.Equal("{892B5932-9AA8-46F9-A857-8967DCDBE4F5}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Equal("HubApp2", solution.ProjectsInOrder[0].ProjectName);
            Assert.False(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[0]));

            Assert.Equal("{A5526AEA-E0A2-496D-94B7-2BBE835C83F8}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Equal("HubApp2.Store", solution.ProjectsInOrder[1].ProjectName);
            Assert.True(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[1]));

            Assert.Equal("{FF6AEDF3-950A-46DD-910B-52BC69B9C99A}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Equal("Shared", solution.ProjectsInOrder[2].ProjectName);
            Assert.False(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[2]));

            Assert.Equal("{024E8607-06B0-440D-8741-5A888DC4B176}", solution.ProjectsInOrder[3].ProjectGuid);
            Assert.Equal("HubApp2.Phone", solution.ProjectsInOrder[3].ProjectName);
            Assert.True(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[3]));

            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[4].ProjectGuid);
            Assert.Equal("MySlnFolder", solution.ProjectsInOrder[4].ProjectName);
            Assert.False(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[4]));

            // Even though it doesn't have project configurations mapped for all solution configurations,
            // it at least has some, so this project should still be marked as "buildable"
            Assert.Equal("{A5EE8128-B08E-4533-86C5-E46714981680}", solution.ProjectsInOrder[5].ProjectGuid);
            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[5].ProjectName);
            Assert.True(SolutionFile.IsBuildableProject(solution.ProjectsInOrder[5]));
        }

        /// <summary>
        /// Verifies that hand-coded project-to-project dependencies listed in the .SLN file
        /// are correctly recognized by our solution parser.
        /// </summary>
        [Fact]
        public void SolutionDependencies()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{05A5AD00-71B5-4612-AF2F-9EA9121C4111}'
                    ProjectSection(ProjectDependencies) = postProject
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9} = {FAB4EE06-6E01-495A-8926-5514599E3DD9}
                    EndProjectSection
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.csproj', '{7F316407-AE3E-4F26-BE61-2C50D30DA158}'
                    ProjectSection(ProjectDependencies) = postProject
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9} = {FAB4EE06-6E01-495A-8926-5514599E3DD9}
                        {05A5AD00-71B5-4612-AF2F-9EA9121C4111} = {05A5AD00-71B5-4612-AF2F-9EA9121C4111}
                    EndProjectSection
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary3', 'ClassLibrary3\ClassLibrary3.csproj', '{FAB4EE06-6E01-495A-8926-5514599E3DD9}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {05A5AD00-71B5-4612-AF2F-9EA9121C4111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {05A5AD00-71B5-4612-AF2F-9EA9121C4111}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {05A5AD00-71B5-4612-AF2F-9EA9121C4111}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {05A5AD00-71B5-4612-AF2F-9EA9121C4111}.Release|Any CPU.Build.0 = Release|Any CPU
                        {7F316407-AE3E-4F26-BE61-2C50D30DA158}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {7F316407-AE3E-4F26-BE61-2C50D30DA158}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {7F316407-AE3E-4F26-BE61-2C50D30DA158}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {7F316407-AE3E-4F26-BE61-2C50D30DA158}.Release|Any CPU.Build.0 = Release|Any CPU
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {FAB4EE06-6E01-495A-8926-5514599E3DD9}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Single(solution.ProjectsInOrder[0].Dependencies);
            Assert.Equal("{FAB4EE06-6E01-495A-8926-5514599E3DD9}", (string)solution.ProjectsInOrder[0].Dependencies[0]);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);
            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[0].GetUniqueProjectName());

            Assert.Equal(@"ClassLibrary2\ClassLibrary2.csproj", solution.ProjectsInOrder[1].RelativePath);
            Assert.Equal("{7F316407-AE3E-4F26-BE61-2C50D30DA158}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Equal(2, solution.ProjectsInOrder[1].Dependencies.Count);
            Assert.Equal("{FAB4EE06-6E01-495A-8926-5514599E3DD9}", (string)solution.ProjectsInOrder[1].Dependencies[0]);
            Assert.Equal("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}", (string)solution.ProjectsInOrder[1].Dependencies[1]);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);
            Assert.Equal("ClassLibrary2", solution.ProjectsInOrder[1].GetUniqueProjectName());

            Assert.Equal(@"ClassLibrary3\ClassLibrary3.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{FAB4EE06-6E01-495A-8926-5514599E3DD9}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);
            Assert.Equal("ClassLibrary3", solution.ProjectsInOrder[2].GetUniqueProjectName());
        }

        /// <summary>
        /// Tests to see that all the data/properties are correctly parsed out of a Venus
        /// project in a .SLN.
        /// </summary>
        [Fact]
        public void VenusProject()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project(`{E24C65DC-7377-472B-9ABA-BC803B73C61A}`) = `C:\WebSites\WebApplication3\`, `C:\WebSites\WebApplication3\`, `{464FD0B9-E335-4677-BE1E-6B2F982F4D86}`
                    ProjectSection(WebsiteProperties) = preProject
                        ProjectReferences = `{FD705688-88D1-4C22-9BFF-86235D89C2FC}|CSCla;ssLibra;ry1.dll;{F0726D09-042B-4A7A-8A01-6BED2422BD5D}|VCClassLibrary1.dll;`
                        Frontpage = false
                         Debug.AspNetCompiler.VirtualPath = `/publishfirst`
                         Debug.AspNetCompiler.PhysicalPath = `..\rajeev\temp\websites\myfirstwebsite\`
                         Debug.AspNetCompiler.TargetPath = `..\rajeev\temp\publishfirst\`
                         Debug.AspNetCompiler.ForceOverwrite = `true`
                         Debug.AspNetCompiler.Updateable = `false`
                         Debug.AspNetCompiler.Debug = `true`
                         Debug.AspNetCompiler.KeyFile = `debugkeyfile.snk`
                         Debug.AspNetCompiler.KeyContainer = `12345.container`
                         Debug.AspNetCompiler.DelaySign = `true`
                         Debug.AspNetCompiler.AllowPartiallyTrustedCallers = `false`
                         Debug.AspNetCompiler.FixedNames = `debugfixednames`
                         Release.AspNetCompiler.VirtualPath = `/publishfirst_release`
                         Release.AspNetCompiler.PhysicalPath = `..\rajeev\temp\websites\myfirstwebsite_release\`
                         Release.AspNetCompiler.TargetPath = `..\rajeev\temp\publishfirst_release\`
                         Release.AspNetCompiler.ForceOverwrite = `true`
                         Release.AspNetCompiler.Updateable = `true`
                         Release.AspNetCompiler.Debug = `false`
                        VWDPort = 63496
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|.NET = Debug|.NET
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {464FD0B9-E335-4677-BE1E-6B2F982F4D86}.Debug|.NET.ActiveCfg = Debug|.NET
                        {464FD0B9-E335-4677-BE1E-6B2F982F4D86}.Debug|.NET.Build.0 = Debug|.NET
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents.Replace('`', '"'));

            Assert.Single(solution.ProjectsInOrder);

            Assert.Equal(SolutionProjectType.WebProject, solution.ProjectsInOrder[0].ProjectType);
            Assert.Equal(@"C:\WebSites\WebApplication3\", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal(@"C:\WebSites\WebApplication3\", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{464FD0B9-E335-4677-BE1E-6B2F982F4D86}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Equal(2, solution.ProjectsInOrder[0].Dependencies.Count);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);
            Assert.Equal(@"C:\WebSites\WebApplication3\", solution.ProjectsInOrder[0].GetUniqueProjectName());

            Hashtable aspNetCompilerParameters = solution.ProjectsInOrder[0].AspNetConfigurations;
            AspNetCompilerParameters debugAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Debug"];
            AspNetCompilerParameters releaseAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Release"];

            Assert.Equal(@"/publishfirst", debugAspNetCompilerParameters.aspNetVirtualPath);
            Assert.Equal(@"..\rajeev\temp\websites\myfirstwebsite\", debugAspNetCompilerParameters.aspNetPhysicalPath);
            Assert.Equal(@"..\rajeev\temp\publishfirst\", debugAspNetCompilerParameters.aspNetTargetPath);
            Assert.Equal(@"true", debugAspNetCompilerParameters.aspNetForce);
            Assert.Equal(@"false", debugAspNetCompilerParameters.aspNetUpdateable);
            Assert.Equal(@"true", debugAspNetCompilerParameters.aspNetDebug);
            Assert.Equal(@"debugkeyfile.snk", debugAspNetCompilerParameters.aspNetKeyFile);
            Assert.Equal(@"12345.container", debugAspNetCompilerParameters.aspNetKeyContainer);
            Assert.Equal(@"true", debugAspNetCompilerParameters.aspNetDelaySign);
            Assert.Equal(@"false", debugAspNetCompilerParameters.aspNetAPTCA);
            Assert.Equal(@"debugfixednames", debugAspNetCompilerParameters.aspNetFixedNames);

            Assert.Equal(@"/publishfirst_release", releaseAspNetCompilerParameters.aspNetVirtualPath);
            Assert.Equal(@"..\rajeev\temp\websites\myfirstwebsite_release\", releaseAspNetCompilerParameters.aspNetPhysicalPath);
            Assert.Equal(@"..\rajeev\temp\publishfirst_release\", releaseAspNetCompilerParameters.aspNetTargetPath);
            Assert.Equal(@"true", releaseAspNetCompilerParameters.aspNetForce);
            Assert.Equal(@"true", releaseAspNetCompilerParameters.aspNetUpdateable);
            Assert.Equal(@"false", releaseAspNetCompilerParameters.aspNetDebug);
            Assert.Equal("", releaseAspNetCompilerParameters.aspNetKeyFile);
            Assert.Equal("", releaseAspNetCompilerParameters.aspNetKeyContainer);
            Assert.Equal("", releaseAspNetCompilerParameters.aspNetDelaySign);
            Assert.Equal("", releaseAspNetCompilerParameters.aspNetAPTCA);
            Assert.Equal("", releaseAspNetCompilerParameters.aspNetFixedNames);

            List<string> aspNetProjectReferences = solution.ProjectsInOrder[0].ProjectReferences;
            Assert.Equal(2, aspNetProjectReferences.Count);
            Assert.Equal("{FD705688-88D1-4C22-9BFF-86235D89C2FC}", aspNetProjectReferences[0]);
            Assert.Equal("{F0726D09-042B-4A7A-8A01-6BED2422BD5D}", aspNetProjectReferences[1]);
        }

        /// <summary>
        /// Tests to see that our solution parser correctly recognizes a Venus project that
        /// sits inside a solution folder.
        /// </summary>
        [Fact]
        public void VenusProjectInASolutionFolder()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\WebSites\WebApplication3\', 'C:\WebSites\WebApplication3\', '{464FD0B9-E335-4677-BE1E-6B2F982F4D86}'
                    ProjectSection(WebsiteProperties) = preProject
                        Frontpage = false
                        AspNetCompiler.VirtualPath = '/webprecompile3'
                        AspNetCompiler.PhysicalPath = '..\..\WebSites\WebApplication3\'
                        AspNetCompiler.TargetPath = '..\..\..\rajeev\temp\webprecompile3\'
                        VWDPort = 63496
                    EndProjectSection
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{092FE6E5-71F8-43F7-9C92-30E3124B8A22}'
                EndProject
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\WebSites\WebApplication4\', 'C:\WebSites\WebApplication4\', '{947DB39C-77BA-4F7F-A667-0BCD59CE853F}'
                    ProjectSection(WebsiteProperties) = preProject
                        Frontpage = false
                        AspNetCompiler.VirtualPath = '/webprecompile4'
                        AspNetCompiler.PhysicalPath = '..\..\WebSites\WebApplication4\'
                        AspNetCompiler.TargetPath = '..\..\..\rajeev\temp\webprecompile4\'
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(NestedProjects) = preSolution
                        {947DB39C-77BA-4F7F-A667-0BCD59CE853F} = {092FE6E5-71F8-43F7-9C92-30E3124B8A22}
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            Assert.Equal(SolutionProjectType.WebProject, solution.ProjectsInOrder[0].ProjectType);
            Assert.Equal(@"C:\WebSites\WebApplication3\", solution.ProjectsInOrder[0].GetUniqueProjectName());

            Assert.Equal(SolutionProjectType.SolutionFolder, solution.ProjectsInOrder[1].ProjectType);
            Assert.Equal("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}", solution.ProjectsInOrder[1].ProjectGuid);

            Assert.Equal(SolutionProjectType.WebProject, solution.ProjectsInOrder[2].ProjectType);
            Assert.Equal(@"C:\WebSites\WebApplication4\", solution.ProjectsInOrder[2].GetUniqueProjectName());
            Assert.Equal("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}", solution.ProjectsInOrder[2].ParentProjectGuid);
        }

        /// <summary>
        /// Make sure the solution configurations get parsed correctly for a simple mixed C#/VC solution
        /// </summary>
        [Fact]
        public void ParseSolutionConfigurations()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|ARM = Debug|ARM
                        Debug|Any CPU = Debug|Any CPU
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Debug|Win32 = Debug|Win32
                        Release|Any CPU = Release|Any CPU
                        Release|Mixed Platforms = Release|Mixed Platforms
                        Release|Win32 = Release|Win32
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|ARM.ActiveCfg = Debug|ARM
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|ARM.Build.0 = Debug|ARM
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Win32.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Mixed Platforms.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Mixed Platforms.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Win32.ActiveCfg = Release|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Any CPU.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.Build.0 = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Win32.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Win32.Build.0 = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.Build.0 = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.Build.0 = Release|Win32
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(7, solution.SolutionConfigurations.Count);

            List<string> configurationNames = new List<string>(6);
            foreach (SolutionConfigurationInSolution configuration in solution.SolutionConfigurations)
            {
                configurationNames.Add(configuration.FullName);
            }

            Assert.Contains("Debug|Any CPU", configurationNames);
            Assert.Contains("Debug|Mixed Platforms", configurationNames);
            Assert.Contains("Debug|Win32", configurationNames);
            Assert.Contains("Release|Any CPU", configurationNames);
            Assert.Contains("Release|Mixed Platforms", configurationNames);
            Assert.Contains("Release|Win32", configurationNames);

            Assert.Equal("Debug", solution.GetDefaultConfigurationName()); // "Default solution configuration"
            Assert.Equal("Mixed Platforms", solution.GetDefaultPlatformName()); // "Default solution platform"
        }

        /// <summary>
        /// Make sure the solution configurations get parsed correctly for a simple C# application
        /// </summary>
        [Fact]
        public void ParseSolutionConfigurationsNoMixedPlatform()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|ARM = Debug|ARM
                        Debug|Any CPU = Debug|Any CPU
                        Debug|x86 = Debug|x86
                        Release|ARM = Release|ARM
                        Release|Any CPU = Release|Any CPU
                        Release|x86 = Release|x86
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|ARM.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|ARM.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|x86.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|ARM.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|ARM.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|x86.ActiveCfg = Release|Any CPU
                   EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(6, solution.SolutionConfigurations.Count);

            List<string> configurationNames = new List<string>(6);
            foreach (SolutionConfigurationInSolution configuration in solution.SolutionConfigurations)
            {
                configurationNames.Add(configuration.FullName);
            }

            Assert.Contains("Debug|Any CPU", configurationNames);
            Assert.Contains("Debug|ARM", configurationNames);
            Assert.Contains("Debug|x86", configurationNames);
            Assert.Contains("Release|Any CPU", configurationNames);
            Assert.Contains("Release|ARM", configurationNames);
            Assert.Contains("Release|x86", configurationNames);

            Assert.Equal("Debug", solution.GetDefaultConfigurationName()); // "Default solution configuration"
            Assert.Equal("Any CPU", solution.GetDefaultPlatformName()); // "Default solution platform"
        }

        /// <summary>
        /// Test some invalid cases for solution configuration parsing.
        /// There can be only one '=' character in a sln cfg entry, separating two identical names
        /// </summary>
        [Fact]
        public void ParseInvalidSolutionConfigurations1()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any=CPU = Debug|Any=CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                EndGlobal
                ";

                ParseSolutionHelper(solutionFileContents);
            }
           );
        }
        /// <summary>
        /// Test some invalid cases for solution configuration parsing
        /// There can be only one '=' character in a sln cfg entry, separating two identical names
        /// </summary>
        [Fact]
        public void ParseInvalidSolutionConfigurations2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Something|Else
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                EndGlobal
                ";

                ParseSolutionHelper(solutionFileContents);
            }
           );
        }
        /// <summary>
        /// Test some invalid cases for solution configuration parsing
        /// Solution configurations must include the platform part
        /// </summary>
        [Fact]
        public void ParseInvalidSolutionConfigurations3()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug = Debug
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                EndGlobal
                ";

                ParseSolutionHelper(solutionFileContents);
            }
           );
        }
        /// <summary>
        /// Make sure the project configurations in solution configurations get parsed correctly
        /// for a simple mixed C#/VC solution
        /// </summary>
        [Fact]
        public void ParseProjectConfigurationsInSolutionConfigurations1()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Debug|Win32 = Debug|Win32
                        Release|Any CPU = Release|Any CPU
                        Release|Mixed Platforms = Release|Mixed Platforms
                        Release|Win32 = Release|Win32
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Win32.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Mixed Platforms.ActiveCfg = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Mixed Platforms.Build.0 = Release|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Win32.ActiveCfg = Release|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Any CPU.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.Build.0 = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Win32.ActiveCfg = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Win32.Build.0 = Debug|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.Build.0 = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.ActiveCfg = Release|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.Build.0 = Release|Win32
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution csProject = (ProjectInSolution)solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution vcProject = (ProjectInSolution)solution.ProjectsByGuid["{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"];

            Assert.Equal(6, csProject.ProjectConfigurations.Count);

            Assert.Equal("Debug|AnyCPU", csProject.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assert.True(csProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName);
            Assert.True(csProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Debug|AnyCPU", csProject.ProjectConfigurations["Debug|Win32"].FullName);
            Assert.False(csProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csProject.ProjectConfigurations["Release|Any CPU"].FullName);
            Assert.True(csProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csProject.ProjectConfigurations["Release|Mixed Platforms"].FullName);
            Assert.True(csProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csProject.ProjectConfigurations["Release|Win32"].FullName);
            Assert.False(csProject.ProjectConfigurations["Release|Win32"].IncludeInBuild);

            Assert.Equal(6, vcProject.ProjectConfigurations.Count);

            Assert.Equal("Debug|Win32", vcProject.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assert.False(vcProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assert.Equal("Debug|Win32", vcProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName);
            Assert.True(vcProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Debug|Win32", vcProject.ProjectConfigurations["Debug|Win32"].FullName);
            Assert.True(vcProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assert.Equal("Release|Win32", vcProject.ProjectConfigurations["Release|Any CPU"].FullName);
            Assert.False(vcProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|Win32", vcProject.ProjectConfigurations["Release|Mixed Platforms"].FullName);
            Assert.True(vcProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Release|Win32", vcProject.ProjectConfigurations["Release|Win32"].FullName);
            Assert.True(vcProject.ProjectConfigurations["Release|Win32"].IncludeInBuild);
        }

        /// <summary>
        /// Make sure the project configurations in solution configurations get parsed correctly
        /// for a more tricky solution
        /// </summary>
        [Fact]
        public void ParseProjectConfigurationsInSolutionConfigurations2()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\solutions\WebSite1\', '..\WebSite1\', '{E8E75132-67E4-4D6F-9CAE-8DA4C883F418}'
                EndProject
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\solutions\WebSite2\', '..\WebSite2\', '{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'NewFolder1', 'NewFolder1', '{54D20FFE-84BE-4066-A51E-B25D040A4235}'
                EndProject
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'NewFolder2', 'NewFolder2', '{D2633E4D-46FF-4C4E-8340-4BC7CDF78615}'
                EndProject
                Project('{8BC9CEB9-8B4A-11D0-8D11-00A0C91BC942}') = 'MSBuild.exe', '..\..\dd\binaries.x86dbg\bin\i386\MSBuild.exe', '{25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|.NET = Debug|.NET
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {E8E75132-67E4-4D6F-9CAE-8DA4C883F418}.Debug|.NET.ActiveCfg = Debug|.NET
                        {E8E75132-67E4-4D6F-9CAE-8DA4C883F418}.Debug|.NET.Build.0 = Debug|.NET
                        {25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0}.Debug|.NET.ActiveCfg = Debug
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0} = {D2633E4D-46FF-4C4E-8340-4BC7CDF78615}
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution webProject = (ProjectInSolution)solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F418}"];
            ProjectInSolution exeProject = (ProjectInSolution)solution.ProjectsByGuid["{25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0}"];
            ProjectInSolution missingWebProject = (ProjectInSolution)solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}"];

            Assert.Single(webProject.ProjectConfigurations);

            Assert.Equal("Debug|.NET", webProject.ProjectConfigurations["Debug|.NET"].FullName);
            Assert.True(webProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild);

            Assert.Single(exeProject.ProjectConfigurations);

            Assert.Equal("Debug", exeProject.ProjectConfigurations["Debug|.NET"].FullName);
            Assert.False(exeProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild);

            Assert.Empty(missingWebProject.ProjectConfigurations);

            Assert.Equal("Debug", solution.GetDefaultConfigurationName()); // "Default solution configuration"
            Assert.Equal(".NET", solution.GetDefaultPlatformName()); // "Default solution platform"
        }
    }
}
