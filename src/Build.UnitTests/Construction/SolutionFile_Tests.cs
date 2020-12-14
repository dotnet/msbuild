// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.Construction
{
    public class SolutionFile_Tests
    {
        public ITestOutputHelper TestOutputHelper { get; }

        public SolutionFile_Tests(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        /// <summary>
        /// Test that a solution filter file is parsed correctly, and it can accurately respond as to whether a project should be filtered out.
        /// </summary>
        [Fact]
        public void ParseSolutionFilter()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder src = testEnvironment.CreateFolder(Path.Combine(folder.Path, "src"), createFolder: true);
                TransientTestFile microsoftBuild = testEnvironment.CreateFile(src, "Microsoft.Build.csproj");
                TransientTestFile msbuild = testEnvironment.CreateFile(src, "MSBuild.csproj");
                TransientTestFile commandLineUnitTests = testEnvironment.CreateFile(src, "Microsoft.Build.CommandLine.UnitTests.csproj");
                TransientTestFile tasksUnitTests = testEnvironment.CreateFile(src, "Microsoft.Build.Tasks.UnitTests.csproj");
                // The important part of this .sln is that it has references to each of the four projects we just created.
                TransientTestFile sln = testEnvironment.CreateFile(folder, "Microsoft.Build.Dev.sln",
                    @"
                    Microsoft Visual Studio Solution File, Format Version 12.00
                    # Visual Studio 15
                    VisualStudioVersion = 15.0.27004.2009
                    MinimumVisualStudioVersion = 10.0.40219.1
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Microsoft.Build"", """ + Path.Combine("src", Path.GetFileName(microsoftBuild.Path)) + @""", ""{69BE05E2-CBDA-4D27-9733-44E12B0F5627}""
                    EndProject
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""MSBuild"", """ + Path.Combine("src", Path.GetFileName(msbuild.Path)) + @""", ""{6F92CA55-1D15-4F34-B1FE-56C0B7EB455E}""
                    EndProject
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Microsoft.Build.CommandLine.UnitTests"", """ + Path.Combine("src", Path.GetFileName(commandLineUnitTests.Path)) + @""", ""{0ADDBC02-0076-4159-B351-2BF33FAA46B2}""
                    EndProject
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Microsoft.Build.Tasks.UnitTests"", """ + Path.Combine("src", Path.GetFileName(tasksUnitTests.Path)) + @""", ""{CF999BDE-02B3-431B-95E6-E88D621D9CBF}""
                    EndProject
                    Global
                        GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        EndGlobalSection
                        GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(ExtensibilityGlobals) = postSolution
                    EndGlobalSection
                    EndGlobal
                    ");
                TransientTestFile slnf = testEnvironment.CreateFile(folder, "Dev.slnf",
                    @"
                    {
                      ""solution"": {
                        ""path"": """ + sln.Path.Replace("\\", "\\\\") + @""",
                        ""projects"": [
                          """ + Path.Combine("src", Path.GetFileName(microsoftBuild.Path)!).Replace("\\", "\\\\") + @""",
                          """ + Path.Combine("src", Path.GetFileName(tasksUnitTests.Path)!).Replace("\\", "\\\\") + @"""
                        ]
                        }
                    }");
                SolutionFile sp = SolutionFile.Parse(slnf.Path);
                sp.ProjectShouldBuild(Path.Combine("src", Path.GetFileName(microsoftBuild.Path)!)).ShouldBeTrue();
                sp.ProjectShouldBuild(Path.Combine("src", Path.GetFileName(tasksUnitTests.Path)!)).ShouldBeTrue();

                
                (sp.ProjectShouldBuild(Path.Combine("src", Path.GetFileName(commandLineUnitTests.Path)!))
                 || sp.ProjectShouldBuild(Path.Combine("src", Path.GetFileName(msbuild.Path)!))
                 || sp.ProjectShouldBuild(Path.Combine("src", "notAProject.csproj")))
                    .ShouldBeFalse();
            }
        }

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
            proj.ProjectType.ShouldBe(SolutionProjectType.Unknown);
            proj.ProjectName.ShouldBe("Project name");
            proj.RelativePath.ShouldBe("Relative path to project file");
            proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
            Should.Throw<InvalidProjectFileException>(() =>
            {
                SolutionFile p = new SolutionFile();
                p.FullPath = "c:\\foo.sln";
                ProjectInSolution proj = new ProjectInSolution(p);

                p.ParseFirstProjectLine
                (
                    "Project(\"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}\") = \"Project name.vcproj\", \"Relative path\\to\\Project name.vcproj\", \"Unique name-GUID\"",
                     proj
                );
            });
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
            proj.ProjectType.ShouldBe(SolutionProjectType.KnownToBeMSBuildFormat);
            proj.ProjectName.ShouldBe("Project name.myvctype");
            proj.RelativePath.ShouldBe("Relative path\\to\\Project name.myvctype");
            proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
            proj.ProjectType.ShouldBe(SolutionProjectType.Unknown);
            proj.ProjectName.ShouldBe("Project name");
            proj.RelativePath.ShouldBe("Relative path to project file");
            proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
            proj.ProjectType.ShouldBe(SolutionProjectType.Unknown);
            proj.ProjectName.ShouldStartWith("EmptyProjectName");
            proj.RelativePath.ShouldBe("src\\.proj");
            proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
                solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"someproj.etp");
                solution.ProjectsInOrder[1].RelativePath.ShouldBe(@"ClassLibrary2.csproj");
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
                ProjectInSolution project = solution.ProjectsByGuid["{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}"];
                ProjectInSolution project2 = solution.ProjectsByGuid["{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}"];
                project.CanBeMSBuildProjectFile(out _).ShouldBeFalse();
                project2.CanBeMSBuildProjectFile(out _).ShouldBeTrue();
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

                SolutionFile solution = ParseSolutionHelper(solutionFileContents);
                ProjectInSolution project1 = solution.ProjectsByGuid["{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}"];
                ProjectInSolution project2 = solution.ProjectsByGuid["{DEA89696-F42B-4B58-B7EE-017FF40817D1}"];

                project1.CanBeMSBuildProjectFile(out _).ShouldBe(false);
                project2.CanBeMSBuildProjectFile(out _).ShouldBe(false);
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
                solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"someproj.etp");
                solution.ProjectsInOrder[1].RelativePath.ShouldBe(@"someproj2.etp");
                solution.ProjectsInOrder[2].RelativePath.ShouldBe(@"ClassLibrary1.csproj");
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

            solutionPriorToDev12.Version.ShouldBe(11);
            solutionPriorToDev12.VisualStudioVersion.ShouldBe(10);

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

            solutionDev12.Version.ShouldBe(11);
            solutionDev12.VisualStudioVersion.ShouldBe(12);

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
            solutionDev12Corrupted1.Version.ShouldBe(11);
            solutionDev12Corrupted1.VisualStudioVersion.ShouldBe(10);

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
            solutionDev12Corrupted2.Version.ShouldBe(11);
            solutionDev12Corrupted2.VisualStudioVersion.ShouldBe(10);

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
            solutionDev12Corrupted3.Version.ShouldBe(11);
            solutionDev12Corrupted3.VisualStudioVersion.ShouldBe(10);

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
            solutionDev12Corrupted4.Version.ShouldBe(11);
            solutionDev12Corrupted4.VisualStudioVersion.ShouldBe(10);

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
            solutionDev12Corrupted5.Version.ShouldBe(11);
            solutionDev12Corrupted5.VisualStudioVersion.ShouldBe(10);

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
            solutionDev12Corrupted6.Version.ShouldBe(11);
            solutionDev12Corrupted6.VisualStudioVersion.ShouldBe(12);
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
                solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"someproj.etp");
                solution.ProjectsInOrder[1].RelativePath.ShouldBe(@"someproj2.etp");
                solution.ProjectsInOrder[2].RelativePath.ShouldBe(@"ETPProjUpgradeTest\someproj3.etp");
                solution.ProjectsInOrder[3].RelativePath.ShouldBe(Path.Combine("ETPProjUpgradeTest", "..", "SomeFolder", "ClassLibrary1.csproj"));
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
                string errCode;
                ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errCode, out _, "Shared.InvalidProjectFile",
                   "someproj.etp", String.Empty);
                foreach (string warningString in solution.SolutionParserWarnings)
                {
                    TestOutputHelper.WriteLine(warningString);
                }
                solution.SolutionParserErrorCodes[0].ShouldContain(errCode);
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
            string errCode;
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errCode, out _, "Shared.ProjectFileCouldNotBeLoaded",
                  "someproj.etp", String.Empty);
            solution.SolutionParserErrorCodes[0].ShouldContain(errCode);
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
            proj.ProjectType.ShouldBe(SolutionProjectType.Unknown);
            proj.ProjectName.ShouldBe("MyProject,(=IsGreat)");
            proj.RelativePath.ShouldBe("Relative path to project file");
            proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
                proj.ProjectType.ShouldBe(SolutionProjectType.Unknown);
                proj.ProjectName.ShouldBe("ProjectInSubdirectory");
                proj.RelativePath.ShouldBe(Path.Combine("RelativePath", "project file"));
                proj.ProjectGuid.ShouldBe("Unique name-GUID");
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
            Should.Throw<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version a.b
                # Visual Studio 2005
                ";

                ParseSolutionHelper(solutionFileContents);
            });
        }
        /// <summary>
        /// Expected version numbers less than 7 to cause an invalid project file exception.
        /// </summary>
        [Fact]
        public void VersionTooLow()
        {
            Should.Throw<InvalidProjectFileException>(() =>
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
            solution.SolutionParserComments.ShouldHaveSingleItem(); // "Expected the solution parser to contain one comment"
            solution.SolutionParserComments[0].ShouldBe(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedSolutionComment", "999"));
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

            solution.Version.ShouldBe(9);
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

            solution.Version.ShouldBe(10);
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
                throw new Exception("Failed to parse solution containing description information. Error: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Tests the parsing of a very basic .SLN file with four independent projects.
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
                Project('{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}') = 'cpsFsProject', 'cpsFsProject\ProjectFileName.fsproj', '{9200923E-1814-4E76-A677-C61E4896D67F}'
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
                        {9200923E-1814-4E76-A677-C61E4896D67F}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {9200923E-1814-4E76-A677-C61E4896D67F}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {9200923E-1814-4E76-A677-C61E4896D67F}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {9200923E-1814-4E76-A677-C61E4896D67F}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            solution.ProjectsInOrder.Count.ShouldBe(4);

            solution.ProjectsInOrder[0].ProjectType.ShouldBe(SolutionProjectType.KnownToBeMSBuildFormat);
            solution.ProjectsInOrder[0].ProjectName.ShouldBe("ConsoleApplication1");
            solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"ConsoleApplication1\ConsoleApplication1.vbproj");
            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{AB3413A6-D689-486D-B7F0-A095371B3F13}");
            solution.ProjectsInOrder[0].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[0].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe("ConsoleApplication1");

            solution.ProjectsInOrder[1].ProjectType.ShouldBe(SolutionProjectType.KnownToBeMSBuildFormat);
            solution.ProjectsInOrder[1].ProjectName.ShouldBe("vbClassLibrary");
            solution.ProjectsInOrder[1].RelativePath.ShouldBe(@"vbClassLibrary\vbClassLibrary.vbproj");
            solution.ProjectsInOrder[1].ProjectGuid.ShouldBe("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}");
            solution.ProjectsInOrder[1].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[1].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[1].GetUniqueProjectName().ShouldBe("vbClassLibrary");

            solution.ProjectsInOrder[2].ProjectType.ShouldBe(SolutionProjectType.KnownToBeMSBuildFormat);
            solution.ProjectsInOrder[2].ProjectName.ShouldBe("ClassLibrary1");
            solution.ProjectsInOrder[2].RelativePath.ShouldBe(@"ClassLibrary1\ClassLibrary1.csproj");
            solution.ProjectsInOrder[2].ProjectGuid.ShouldBe("{DEBCE986-61B9-435E-8018-44B9EF751655}");
            solution.ProjectsInOrder[2].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[2].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[2].GetUniqueProjectName().ShouldBe("ClassLibrary1");

            solution.ProjectsInOrder[3].ProjectType.ShouldBe(SolutionProjectType.KnownToBeMSBuildFormat);
            solution.ProjectsInOrder[3].ProjectName.ShouldBe("cpsFsProject");
            solution.ProjectsInOrder[3].RelativePath.ShouldBe(@"cpsFsProject\ProjectFileName.fsproj");
            solution.ProjectsInOrder[3].ProjectGuid.ShouldBe("{9200923E-1814-4E76-A677-C61E4896D67F}");
            solution.ProjectsInOrder[3].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[3].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[3].GetUniqueProjectName().ShouldBe("cpsFsProject");
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

            solution.ProjectsInOrder.Count.ShouldBe(5);

            solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"ClassLibrary1\ClassLibrary1.csproj");
            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{34E0D07D-CF8F-459D-9449-C4188D8C5564}");
            solution.ProjectsInOrder[0].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[0].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe("ClassLibrary1");

            solution.ProjectsInOrder[1].ProjectType.ShouldBe(SolutionProjectType.SolutionFolder);
            solution.ProjectsInOrder[1].ProjectGuid.ShouldBe("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}");
            solution.ProjectsInOrder[1].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[1].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[1].GetUniqueProjectName().ShouldBe("MySlnFolder");

            solution.ProjectsInOrder[2].RelativePath.ShouldBe(@"MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj");
            solution.ProjectsInOrder[2].ProjectGuid.ShouldBe("{A5EE8128-B08E-4533-86C5-E46714981680}");
            solution.ProjectsInOrder[2].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[2].ParentProjectGuid.ShouldBe("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}");
            solution.ProjectsInOrder[2].GetUniqueProjectName().ShouldBe(@"MySlnFolder\ClassLibrary1");

            solution.ProjectsInOrder[3].ProjectType.ShouldBe(SolutionProjectType.SolutionFolder);
            solution.ProjectsInOrder[3].ProjectGuid.ShouldBe("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}");
            solution.ProjectsInOrder[3].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[3].ParentProjectGuid.ShouldBe("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}");
            solution.ProjectsInOrder[3].GetUniqueProjectName().ShouldBe(@"MySlnFolder\MySubSlnFolder");

            solution.ProjectsInOrder[4].RelativePath.ShouldBe(@"ClassLibrary2\ClassLibrary2.csproj");
            solution.ProjectsInOrder[4].ProjectGuid.ShouldBe("{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}");
            solution.ProjectsInOrder[4].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[4].ParentProjectGuid.ShouldBe("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}");
            solution.ProjectsInOrder[4].GetUniqueProjectName().ShouldBe(@"MySlnFolder\MySubSlnFolder\ClassLibrary2");
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

            InvalidProjectFileException e = Should.Throw<InvalidProjectFileException>(() =>
            {
                ParseSolutionHelper(solutionFileContents);
            });

            e.ErrorCode.ShouldBe("MSB5023");
            e.Message.ShouldContain("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}");
        }

        /// <summary>
        /// Checks whether incorrect nesting found within the solution file is reported MSB5009 error
        /// with the incorrectly nested project's name and it's GUID
        /// </summary>
        [Fact]
        public void IncorrectlyNestedProjectErrorContainsProjectNameAndGuid()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'SolutionFolder', 'SolutionFolder', '{5EE89BD0-04E3-4600-9CF2-D083A77A9448}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ConsoleApp1', 'ConsoleApp1\ConsoleApp1.csproj', '{1484A47E-F4C5-4700-B13F-A2BDB6ADD35E}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {1484A47E-F4C5-4700-B13F-A2BDB6ADD35E}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {1484A47E-F4C5-4700-B13F-A2BDB6ADD35E}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {1484A47E-F4C5-4700-B13F-A2BDB6ADD35E} = {5EE89BD0-04E3-4600-9CF2-D083A77A9448}
                        {1484A47E-F4C5-4700-B13F-A2BDB6ADD35E} = {5EE89BD0-04E3-4600-9CF2-D083A77A9449}
                    EndGlobalSection
                    GlobalSection(ExtensibilityGlobals) = postSolution
                        SolutionGuid = {AF600A67-B616-453E-9B27-4407D654F66E}
                    EndGlobalSection
                EndGlobal
                ";

            InvalidProjectFileException e = Should.Throw<InvalidProjectFileException>(() => ParseSolutionHelper(solutionFileContents));

            e.ErrorCode.ShouldBe("MSB5009");
            e.Message.ShouldContain("{1484A47E-F4C5-4700-B13F-A2BDB6ADD35E}");
            e.Message.ShouldContain("ConsoleApp1");
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

            solution.ProjectsInOrder.Count.ShouldBe(6);

            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{892B5932-9AA8-46F9-A857-8967DCDBE4F5}");
            solution.ProjectsInOrder[0].ProjectName.ShouldBe("HubApp2");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[0]).ShouldBeFalse();

            solution.ProjectsInOrder[1].ProjectGuid.ShouldBe("{A5526AEA-E0A2-496D-94B7-2BBE835C83F8}");
            solution.ProjectsInOrder[1].ProjectName.ShouldBe("HubApp2.Store");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[1]).ShouldBeTrue();

            solution.ProjectsInOrder[2].ProjectGuid.ShouldBe("{FF6AEDF3-950A-46DD-910B-52BC69B9C99A}");
            solution.ProjectsInOrder[2].ProjectName.ShouldBe("Shared");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[2]).ShouldBeFalse();

            solution.ProjectsInOrder[3].ProjectGuid.ShouldBe("{024E8607-06B0-440D-8741-5A888DC4B176}");
            solution.ProjectsInOrder[3].ProjectName.ShouldBe("HubApp2.Phone");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[3]).ShouldBeTrue();

            solution.ProjectsInOrder[4].ProjectGuid.ShouldBe("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}");
            solution.ProjectsInOrder[4].ProjectName.ShouldBe("MySlnFolder");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[4]).ShouldBeFalse();

            // Even though it doesn't have project configurations mapped for all solution configurations,
            // it at least has some, so this project should still be marked as "buildable"
            solution.ProjectsInOrder[5].ProjectGuid.ShouldBe("{A5EE8128-B08E-4533-86C5-E46714981680}");
            solution.ProjectsInOrder[5].ProjectName.ShouldBe("ClassLibrary1");
            SolutionFile.IsBuildableProject(solution.ProjectsInOrder[5]).ShouldBeTrue();
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

            solution.ProjectsInOrder.Count.ShouldBe(3);

            solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"ClassLibrary1\ClassLibrary1.csproj");
            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}");
            solution.ProjectsInOrder[0].Dependencies.ShouldHaveSingleItem();
            solution.ProjectsInOrder[0].Dependencies[0].ShouldBe("{FAB4EE06-6E01-495A-8926-5514599E3DD9}");
            solution.ProjectsInOrder[0].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe("ClassLibrary1");

            solution.ProjectsInOrder[1].RelativePath.ShouldBe(@"ClassLibrary2\ClassLibrary2.csproj");
            solution.ProjectsInOrder[1].ProjectGuid.ShouldBe("{7F316407-AE3E-4F26-BE61-2C50D30DA158}");
            solution.ProjectsInOrder[1].Dependencies.Count.ShouldBe(2);
            solution.ProjectsInOrder[1].Dependencies[0].ShouldBe("{FAB4EE06-6E01-495A-8926-5514599E3DD9}");
            solution.ProjectsInOrder[1].Dependencies[1].ShouldBe("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}");
            solution.ProjectsInOrder[1].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[1].GetUniqueProjectName().ShouldBe("ClassLibrary2");

            solution.ProjectsInOrder[2].RelativePath.ShouldBe(@"ClassLibrary3\ClassLibrary3.csproj");
            solution.ProjectsInOrder[2].ProjectGuid.ShouldBe("{FAB4EE06-6E01-495A-8926-5514599E3DD9}");
            solution.ProjectsInOrder[2].Dependencies.ShouldBeEmpty();
            solution.ProjectsInOrder[2].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[2].GetUniqueProjectName().ShouldBe("ClassLibrary3");
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

            solution.ProjectsInOrder.ShouldHaveSingleItem();

            solution.ProjectsInOrder[0].ProjectType.ShouldBe(SolutionProjectType.WebProject);
            solution.ProjectsInOrder[0].ProjectName.ShouldBe(@"C:\WebSites\WebApplication3\");
            solution.ProjectsInOrder[0].RelativePath.ShouldBe(@"C:\WebSites\WebApplication3\");
            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{464FD0B9-E335-4677-BE1E-6B2F982F4D86}");
            solution.ProjectsInOrder[0].Dependencies.Count.ShouldBe(2);
            solution.ProjectsInOrder[0].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe(@"C:\WebSites\WebApplication3\");

            Hashtable aspNetCompilerParameters = solution.ProjectsInOrder[0].AspNetConfigurations;
            AspNetCompilerParameters debugAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Debug"];
            AspNetCompilerParameters releaseAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Release"];

            debugAspNetCompilerParameters.aspNetVirtualPath.ShouldBe(@"/publishfirst");
            debugAspNetCompilerParameters.aspNetPhysicalPath.ShouldBe(@"..\rajeev\temp\websites\myfirstwebsite\");
            debugAspNetCompilerParameters.aspNetTargetPath.ShouldBe(@"..\rajeev\temp\publishfirst\");
            debugAspNetCompilerParameters.aspNetForce.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetUpdateable.ShouldBe(@"false");
            debugAspNetCompilerParameters.aspNetDebug.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetKeyFile.ShouldBe(@"debugkeyfile.snk");
            debugAspNetCompilerParameters.aspNetKeyContainer.ShouldBe(@"12345.container");
            debugAspNetCompilerParameters.aspNetDelaySign.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetAPTCA.ShouldBe(@"false");
            debugAspNetCompilerParameters.aspNetFixedNames.ShouldBe(@"debugfixednames");

            releaseAspNetCompilerParameters.aspNetVirtualPath.ShouldBe(@"/publishfirst_release");
            releaseAspNetCompilerParameters.aspNetPhysicalPath.ShouldBe(@"..\rajeev\temp\websites\myfirstwebsite_release\");
            releaseAspNetCompilerParameters.aspNetTargetPath.ShouldBe(@"..\rajeev\temp\publishfirst_release\");
            releaseAspNetCompilerParameters.aspNetForce.ShouldBe(@"true");
            releaseAspNetCompilerParameters.aspNetUpdateable.ShouldBe(@"true");
            releaseAspNetCompilerParameters.aspNetDebug.ShouldBe(@"false");
            releaseAspNetCompilerParameters.aspNetKeyFile.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetKeyContainer.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetDelaySign.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetAPTCA.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetFixedNames.ShouldBe("");

            List<string> aspNetProjectReferences = solution.ProjectsInOrder[0].ProjectReferences;
            aspNetProjectReferences.Count.ShouldBe(2);
            aspNetProjectReferences[0].ShouldBe("{FD705688-88D1-4C22-9BFF-86235D89C2FC}");
            aspNetProjectReferences[1].ShouldBe("{F0726D09-042B-4A7A-8A01-6BED2422BD5D}");
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

            solution.ProjectsInOrder.Count.ShouldBe(3);

            solution.ProjectsInOrder[0].ProjectType.ShouldBe(SolutionProjectType.WebProject);
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe(@"C:\WebSites\WebApplication3\");

            solution.ProjectsInOrder[1].ProjectType.ShouldBe(SolutionProjectType.SolutionFolder);
            solution.ProjectsInOrder[1].ProjectGuid.ShouldBe("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}");

            solution.ProjectsInOrder[2].ProjectType.ShouldBe(SolutionProjectType.WebProject);
            solution.ProjectsInOrder[2].GetUniqueProjectName().ShouldBe(@"C:\WebSites\WebApplication4\");
            solution.ProjectsInOrder[2].ParentProjectGuid.ShouldBe("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}");
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

            solution.SolutionConfigurations.Count.ShouldBe(7);

            List<string> configurationNames = new List<string>(6);
            foreach (SolutionConfigurationInSolution configuration in solution.SolutionConfigurations)
            {
                configurationNames.Add(configuration.FullName);
            }

            configurationNames.ShouldContain("Debug|Any CPU");
            configurationNames.ShouldContain("Debug|Mixed Platforms");
            configurationNames.ShouldContain("Debug|Win32");
            configurationNames.ShouldContain("Release|Any CPU");
            configurationNames.ShouldContain("Release|Mixed Platforms");
            configurationNames.ShouldContain("Release|Win32");

            solution.GetDefaultConfigurationName().ShouldBe("Debug"); // "Default solution configuration"
            solution.GetDefaultPlatformName().ShouldBe("Mixed Platforms"); // "Default solution platform"
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

            solution.SolutionConfigurations.Count.ShouldBe(6);

            List<string> configurationNames = new List<string>(6);
            foreach (SolutionConfigurationInSolution configuration in solution.SolutionConfigurations)
            {
                configurationNames.Add(configuration.FullName);
            }

            configurationNames.ShouldContain("Debug|Any CPU");
            configurationNames.ShouldContain("Debug|ARM");
            configurationNames.ShouldContain("Debug|x86");
            configurationNames.ShouldContain("Release|Any CPU");
            configurationNames.ShouldContain("Release|ARM");
            configurationNames.ShouldContain("Release|x86");

            solution.GetDefaultConfigurationName().ShouldBe("Debug"); // "Default solution configuration"
            solution.GetDefaultPlatformName().ShouldBe("Any CPU"); // "Default solution platform"
        }

        /// <summary>
        /// Test some invalid cases for solution configuration parsing.
        /// There can be only one '=' character in a sln cfg entry, separating two identical names
        /// </summary>
        [Fact]
        public void ParseInvalidSolutionConfigurations1()
        {
            Should.Throw<InvalidProjectFileException>(() =>
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
            Should.Throw<InvalidProjectFileException>(() =>
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
            Should.Throw<InvalidProjectFileException>(() =>
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
        /// Test some invalid cases for solution configuration parsing
        /// Each project in the solution should end with EndProject.
        /// If it doesn't then each next project should still be parsed correctly.
        /// Which means even if a project is missing it's EndProject, next projects are still found and are parsed correctly.
        /// </summary>
        [Fact]
        public void ParseAllProjectsContainedInInvalidSolutionEvenWhenMissingEndProject()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary', 'ClassLibrary\ClassLibrary.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'SomeLowLevelLayerProject', 'Layers\SomeLowLevelLayerProject.csproj', '{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}'
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'SomeHighLevelLayerProject', 'Layers\SomeHighLevelLayerProject.csproj', '{D2633E4D-46FF-4C4E-8340-4BC7CDF78615}'
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|x86 = Debug|x86
                        Release|x86 = Release|x86
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|x86.ActiveCfg = Debug|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|x86.ActiveCfg = Release|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|x86.ActiveCfg = Debug|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|x86.ActiveCfg = Release|Any CPU
                        {E8E75132-67E4-4D6F-9CAE-8DA4C883F419}.Debug|x86.ActiveCfg = Debug|Any CPU
                        {E8E75132-67E4-4D6F-9CAE-8DA4C883F419}.Release|x86.ActiveCfg = Release|Any CPU
                        {D2633E4D-46FF-4C4E-8340-4BC7CDF78615}.Debug|x86.ActiveCfg = Debug|Any CPU
                        {D2633E4D-46FF-4C4E-8340-4BC7CDF78615}.Release|x86.ActiveCfg = Release|Any CPU
                   EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            // What is needed to be checked is whether there were still both projects found in the invalid solution file
            ProjectInSolution classLibraryProject = solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution mainAppProject = solution.ProjectsByGuid["{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"];
            ProjectInSolution lowLevelProject = solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}"];
            ProjectInSolution highLevelProject = solution.ProjectsByGuid["{D2633E4D-46FF-4C4E-8340-4BC7CDF78615}"];
            mainAppProject.GetUniqueProjectName().ShouldNotBe(classLibraryProject.GetUniqueProjectName());
            classLibraryProject.GetUniqueProjectName().ShouldBe("ClassLibrary");
            mainAppProject.GetUniqueProjectName().ShouldBe("MainApp");
            lowLevelProject.GetUniqueProjectName().ShouldNotBe(highLevelProject.GetUniqueProjectName());
            lowLevelProject.GetUniqueProjectName().ShouldBe("SomeLowLevelLayerProject");
            highLevelProject.GetUniqueProjectName().ShouldBe("SomeHighLevelLayerProject");
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

            ProjectInSolution csProject = solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution vcProject = solution.ProjectsByGuid["{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"];

            csProject.ProjectConfigurations.Count.ShouldBe(6);

            csProject.ProjectConfigurations["Debug|Any CPU"].FullName.ShouldBe("Debug|AnyCPU");
            csProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild.ShouldBeTrue();

            csProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName.ShouldBe("Release|AnyCPU");
            csProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild.ShouldBeTrue();

            csProject.ProjectConfigurations["Debug|Win32"].FullName.ShouldBe("Debug|AnyCPU");
            csProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild.ShouldBeFalse();

            csProject.ProjectConfigurations["Release|Any CPU"].FullName.ShouldBe("Release|AnyCPU");
            csProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild.ShouldBeTrue();

            csProject.ProjectConfigurations["Release|Mixed Platforms"].FullName.ShouldBe("Release|AnyCPU");
            csProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild.ShouldBeTrue();

            csProject.ProjectConfigurations["Release|Win32"].FullName.ShouldBe("Release|AnyCPU");
            csProject.ProjectConfigurations["Release|Win32"].IncludeInBuild.ShouldBeFalse();

            vcProject.ProjectConfigurations.Count.ShouldBe(6);

            vcProject.ProjectConfigurations["Debug|Any CPU"].FullName.ShouldBe("Debug|Win32");
            vcProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild.ShouldBeFalse();

            vcProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName.ShouldBe("Debug|Win32");
            vcProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild.ShouldBeTrue();

            vcProject.ProjectConfigurations["Debug|Win32"].FullName.ShouldBe("Debug|Win32");
            vcProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild.ShouldBeTrue();

            vcProject.ProjectConfigurations["Release|Any CPU"].FullName.ShouldBe("Release|Win32");
            vcProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild.ShouldBeFalse();

            vcProject.ProjectConfigurations["Release|Mixed Platforms"].FullName.ShouldBe("Release|Win32");
            vcProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild.ShouldBeTrue();

            vcProject.ProjectConfigurations["Release|Win32"].FullName.ShouldBe("Release|Win32");
            vcProject.ProjectConfigurations["Release|Win32"].IncludeInBuild.ShouldBeTrue();
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

            ProjectInSolution webProject = solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F418}"];
            ProjectInSolution exeProject = solution.ProjectsByGuid["{25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0}"];
            ProjectInSolution missingWebProject = solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}"];

            webProject.ProjectConfigurations.ShouldHaveSingleItem();

            webProject.ProjectConfigurations["Debug|.NET"].FullName.ShouldBe("Debug|.NET");
            webProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild.ShouldBeTrue();

            exeProject.ProjectConfigurations.ShouldHaveSingleItem();

            exeProject.ProjectConfigurations["Debug|.NET"].FullName.ShouldBe("Debug");
            exeProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild.ShouldBeFalse();

            missingWebProject.ProjectConfigurations.ShouldBeEmpty();

            solution.GetDefaultConfigurationName().ShouldBe("Debug"); // "Default solution configuration"
            solution.GetDefaultPlatformName().ShouldBe(".NET"); // "Default solution platform"
        }

        [Fact]
        public void ParseSolutionFileContainingProjectsWithParentSlnFolder()
        {
            string solutionFileContents = @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'MySlnFolder', 'MySlnFolder', '{E0F97730-25D2-418A-A7BD-02CAFDC6E470}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'MyPhysicalFolder\Folder1\Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'MyPhysicalFolder\Folder2\Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
		        {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		        {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		        {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		        {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {FC2889D9-6050-4D2E-B022-979CCFEEAAAC} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                        {ED30D4A3-1214-410B-82BB-B61E5A9D05CA} = {E0F97730-25D2-418A-A7BD-02CAFDC6E470}
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution project1 = solution.ProjectsByGuid["{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}"];
            ProjectInSolution project2 = solution.ProjectsByGuid["{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}"];

            project2.GetUniqueProjectName().ShouldNotBe(project1.GetUniqueProjectName());
            project1.GetUniqueProjectName().ShouldBe(@"MySlnFolder\Project_Named_With_Dots");
            project2.GetUniqueProjectName().ShouldBe(@"MySlnFolder\Project_Named_With_Dots_ED30D4A3-1214-410B-82BB-B61E5A9D05CA");
            project1.GetOriginalProjectName().ShouldBe(@"MySlnFolder\Project.Named.With.Dots");
            project2.GetOriginalProjectName().ShouldBe(@"MySlnFolder\Project_Named_With_Dots");
        }

        [Theory]
        [InlineData(@"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ")]
        [InlineData(@"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ")]
        public void ParseSolutionFileContainingProjectsWithSimilarNames_TwoProjects(string solutionFileContents)
        {
            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution project1 = solution.ProjectsByGuid["{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}"];
            ProjectInSolution project2 = solution.ProjectsByGuid["{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}"];

            project2.GetUniqueProjectName().ShouldNotBe(project1.GetUniqueProjectName());
            project1.GetUniqueProjectName().ShouldBe("Project_Named_With_Dots_FC2889D9-6050-4D2E-B022-979CCFEEAAAC");
            project2.GetUniqueProjectName().ShouldBe("Project_Named_With_Dots");
            project1.GetOriginalProjectName().ShouldBe("Project.Named.With.Dots");
            project2.GetOriginalProjectName().ShouldBe("Project_Named_With_Dots");
        }

        [Theory]
        [InlineData(@"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With.Dots', 'Project_Named_With.Dots.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ")]
        [InlineData(@"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With.Dots', 'Project_Named_With.Dots.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ")]
        public void ParseSolutionFileContainingProjectsWithSimilarNames_ThreeProjects(string solutionFileContents)
        {
            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution project1 = solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution project2 = solution.ProjectsByGuid["{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}"];
            ProjectInSolution project3 = solution.ProjectsByGuid["{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}"];

            project2.GetUniqueProjectName().ShouldNotBe(project1.GetUniqueProjectName());
            project3.GetUniqueProjectName().ShouldNotBe(project2.GetUniqueProjectName());
            project3.GetUniqueProjectName().ShouldNotBe(project1.GetUniqueProjectName());

            project1.GetUniqueProjectName().ShouldBe("Project_Named_With_Dots_6185CC21-BE89-448A-B3C0-D1C27112E595");
            project2.GetUniqueProjectName().ShouldBe("Project_Named_With_Dots_FC2889D9-6050-4D2E-B022-979CCFEEAAAC");
            project3.GetUniqueProjectName().ShouldBe("Project_Named_With_Dots");

            project1.GetOriginalProjectName().ShouldBe("Project_Named_With.Dots");
            project2.GetOriginalProjectName().ShouldBe("Project.Named.With.Dots");
            project3.GetOriginalProjectName().ShouldBe("Project_Named_With_Dots");
        }

        [Fact]
        public void ParseSolutionFileContainingProjectsWithSimilarNames_ThreeProjects_OneNormalizedDuplicated()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ";

            Action parseSolution = () => ParseSolutionHelper(solutionFileContents);
            var exception = Should.Throw<InvalidProjectFileException>(parseSolution);

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, "SolutionParseDuplicateProject", "Project.Named.With.Dots");

            exception.Message.ShouldStartWith(message);
        }

        [Fact]
        public void ParseSolutionFileContainingProjectsWithSimilarNames_ThreeProjects_OneDuplicated()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ";

            Action parseSolution = () => ParseSolutionHelper(solutionFileContents);
            var exception = Should.Throw<InvalidProjectFileException>(parseSolution);

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, "SolutionParseDuplicateProject", "Project_Named_With_Dots");

            exception.Message.ShouldStartWith(message);
        }

        [Fact]
        public void ParseSolutionFileContainingProjectsWithSimilarNames_FourProjects_OneDuplicated()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27130.2010
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project.Named.With.Dots', 'Project.Named.With.Dots.csproj', '{FC2889D9-6050-4D2E-B022-979CCFEEAAAC}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{ED30D4A3-1214-410B-82BB-B61E5A9D05CA}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With.Dots', 'Project_Named_With.Dots.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Project_Named_With_Dots', 'Project_Named_With_Dots.csproj', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
                EndProject
                Global
	                GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                Release|Any CPU = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {FC2889D9-6050-4D2E-B022-979CCFEEAAAC}.Release|Any CPU.Build.0 = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {ED30D4A3-1214-410B-82BB-B61E5A9D05CA}.Release|Any CPU.Build.0 = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.Build.0 = Release|Any CPU
	                EndGlobalSection
	                GlobalSection(SolutionProperties) = preSolution
		                HideSolutionNode = FALSE
	                EndGlobalSection
	                GlobalSection(ExtensibilityGlobals) = postSolution
		                SolutionGuid = {C038ED6B-BFC1-4E50-AE2E-7993F6883D7F}
	                EndGlobalSection
                EndGlobal
                ";

            Action parseSolution = () => ParseSolutionHelper(solutionFileContents);
            var exception = Should.Throw<InvalidProjectFileException>(parseSolution);

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, "SolutionParseDuplicateProject", "Project_Named_With_Dots");

            exception.Message.ShouldStartWith(message);
        }

        /// <summary>
        /// A test where paths contain ..\ segments to ensure the paths are normalized.
        /// </summary>
        [Fact]
        public void ParseSolutionWithParentedPaths()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{749ABBD6-B803-4DA5-8209-498127164114}')  = 'ProjectA',  '..\ProjectA\ProjectA.csproj', '{0ABED153-9451-483C-8140-9E8D7306B216}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|AnyCPU = Debug|AnyCPU
                        Release|AnyCPU = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {0ABED153-9451-483C-8140-9E8D7306B216}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {0ABED153-9451-483C-8140-9E8D7306B216}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {0ABED153-9451-483C-8140-9E8D7306B216}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {0ABED153-9451-483C-8140-9E8D7306B216}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);
            string expectedRelativePath = Path.Combine("..", "ProjectA", "ProjectA.csproj");
            solution.ProjectsInOrder[0].ProjectName.ShouldBe("ProjectA");
            solution.ProjectsInOrder[0].RelativePath.ShouldBe(expectedRelativePath);
            solution.ProjectsInOrder[0].AbsolutePath.ShouldBe(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution.FullPath)!, expectedRelativePath)));
            solution.ProjectsInOrder[0].ProjectGuid.ShouldBe("{0ABED153-9451-483C-8140-9E8D7306B216}");
        }
    }
}
