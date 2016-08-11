// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class SolutionParser_Tests
    {
        /// <summary>
        /// Test just the most basic, plain vanilla first project line.
        /// </summary>
        /// <owner>JomoF</owner>
        [Test]
        public void BasicParseFirstProjectLine()
        {
            SolutionParser p = new SolutionParser();
            p.SolutionFile = "foobar.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{Project GUID}\") = \"Project name\", \"Relative path to project file\", \"Unique name-GUID\"", 
                 proj
            );
            Assertion.AssertEquals(SolutionProjectType.Unknown, proj.ProjectType);
            Assertion.AssertEquals("Project name", proj.ProjectName);
            Assertion.AssertEquals("Relative path to project file", proj.RelativePath);
            Assertion.AssertEquals("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// A slightly more complicated test where there is some different whitespace.
        /// </summary>
        /// <owner>JomoF</owner>
        [Test]
        public void ParseFirstProjectLineWithDifferentSpacing()
        {
            SolutionParser p = new SolutionParser();
            p.SolutionFile = "foobar.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\" {Project GUID} \")  = \" Project name \",  \" Relative path to project file \"    , \" Unique name-GUID \"", 
                 proj
            );
            Assertion.AssertEquals(SolutionProjectType.Unknown, proj.ProjectType);
            Assertion.AssertEquals("Project name", proj.ProjectName);
            Assertion.AssertEquals("Relative path to project file", proj.RelativePath);
            Assertion.AssertEquals("Unique name-GUID", proj.ProjectGuid);
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        /// <owner>NazanKa</owner>
        [Test]
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

                // Create the SolutionParser object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
	                        ProjectSection(ProjectDependencies) = postProject
	                        EndProjectSection
                        EndProject";
                SolutionParser solution = ParseSolutionHelper(solutionFileContents);
                 //Project should get added to the solution
                Assertion.AssertEquals(solution.Projects[0].RelativePath, @"someproj.etp");
                Assertion.AssertEquals(solution.Projects[1].RelativePath, @"ClassLibrary2.csproj");
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
        /// <owner>NazanKa</owner>
        [Test]
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

                string genericProj= @"
                <Project ToolsVersion=""3.5"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
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
                ";

                File.WriteAllText(proj1Path, etpProjContent);
                File.WriteAllText(proj2Path, genericProj);

                // Create the SolutionParser object
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


                SolutionParser solution = ParseSolutionHelper(solutionFileContents);
                ProjectInSolution project = (ProjectInSolution)solution.ProjectsByGuid["{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}"];
                ProjectInSolution project2 = (ProjectInSolution)solution.ProjectsByGuid["{CCCCCCCC-9925-4D57-9DAF-E0A9D936ABDB}"];
                string error = null;
                Assertion.Assert(!project.CanBeMSBuildProjectFile(out error));
                Assertion.Assert(project2.CanBeMSBuildProjectFile(out error));
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
                File.Delete(proj2Path);
            }
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        /// <owner>NazanKa</owner>
        [Test]
        public void ParseNestedEtpProjectSingleLevel()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            string proj2Path = Path.Combine(Path.GetTempPath(),"someproj2.etp");
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

                // Create the SolutionParser object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
	                        ProjectSection(ProjectDependencies) = postProject
	                        EndProjectSection
                        EndProject";
                SolutionParser solution = ParseSolutionHelper(solutionFileContents);

                //Project should get added to the solution
                Assertion.AssertEquals(solution.Projects[0].RelativePath, @"someproj.etp");
                Assertion.AssertEquals(solution.Projects[1].RelativePath, @"someproj2.etp");
                Assertion.AssertEquals(solution.Projects[2].RelativePath, @"ClassLibrary1.csproj");
            }
            // Delete the files created during the test
            finally
            {
                File.Delete(proj1Path);
                File.Delete(proj2Path);
            }
        }

        /// <summary>
        /// Test ParseEtpProject function.
        /// </summary>
        /// <owner>NazanKa</owner>
        [Test]
        public void ParseNestedEtpProjectMultipleLevel()
        {
            string proj1Path = Path.Combine(Path.GetTempPath(), "someproj.etp");
            string proj2Path = Path.Combine(Path.GetTempPath(), "someproj2.etp");
            string proj3Path = Path.Combine(Path.GetTempPath(), "ETPProjUpgradeTest" + Path.DirectorySeparatorChar + "someproj3.etp");
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

                // Create the thirsd .etp project file
                etpProjContent = @"<?xml version=""1.0""?>
                <EFPROJECT>
                    <GENERAL>
                        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                        <VERSION>1.00</VERSION>
                        <References>
                            <Reference>
                                <FILE>..\SomeFolder\ClassLibrary1.csproj</FILE>
                                <GUIDPROJECTID>{83D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FF}</GUIDPROJECTID>
                            </Reference>
                        </References>
                    </GENERAL>
                </EFPROJECT>";
                //Create the directory for the third project
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ETPProjUpgradeTest"));
                File.WriteAllText(proj3Path, etpProjContent);

                // Create the SolutionParser object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
	                        ProjectSection(ProjectDependencies) = postProject
	                        EndProjectSection
                        EndProject";
                SolutionParser solution = ParseSolutionHelper(solutionFileContents);

                //Project should get added to the solution
                Assertion.AssertEquals(solution.Projects[0].RelativePath, @"someproj.etp");
                Assertion.AssertEquals(solution.Projects[1].RelativePath, @"someproj2.etp");
                Assertion.AssertEquals(solution.Projects[2].RelativePath, @"ETPProjUpgradeTest\someproj3.etp");
                Assertion.AssertEquals(solution.Projects[3].RelativePath, @"ETPProjUpgradeTest\..\SomeFolder\ClassLibrary1.csproj");
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
        [Test]
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

                // Create the SolutionParser object
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 8.00
                        Project('{FE3BBBB6-72D5-11D2-9ACE-00C04F79A2A4}') = 'someproj', 'someproj.etp', '{AD0F3D02-9925-4D57-9DAF-E0A9D936ABDB}'
	                        ProjectSection(ProjectDependencies) = postProject
	                        EndProjectSection
                        EndProject";
                SolutionParser solution = ParseSolutionHelper(solutionFileContents);
                string errCode, ignoredKeyword;
                ResourceUtilities.FormatResourceString(out errCode, out ignoredKeyword, "Shared.InvalidProjectFile",
                   "someproj.etp", String.Empty);
                foreach (string warningString in solution.SolutionParserWarnings)
                {
                    Console.WriteLine(warningString.ToString());
                }
                Assertion.Assert(solution.SolutionParserErrorCodes[0].ToString().Contains(errCode));
            }
            // Delete the files created suring the test
            finally
            {
                File.Delete(proj1Path);
            }
        }

        /// <summary>
        /// Ensure that a missing .etp proj file listed in the .SLN file results in an
        /// InvalidProjectFileException.
        /// </summary>
        [Test]
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
            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
            string errCode, ignoredKeyword;
            ResourceUtilities.FormatResourceString(out errCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                  "someproj.etp", String.Empty);
            Assertion.Assert(solution.SolutionParserErrorCodes[0].ToString().Contains(errCode));
        }

        /// <summary>
        /// Test some characters that are valid in a file name but that also could be
        /// considered a delimiter by a parser. Does quoting work for special characters?
        /// </summary>
        /// <owner>JomoF</owner>
        [Test]
        public void ParseFirstProjectLineWhereProjectNameHasSpecialCharacters()
        {
            SolutionParser p = new SolutionParser();
            p.SolutionFile = "foobar.sln";
            ProjectInSolution proj = new ProjectInSolution(p);

            p.ParseFirstProjectLine
            (
                "Project(\"{Project GUID}\")  = \"MyProject,(=IsGreat)\",  \"Relative path to project file\"    , \"Unique name-GUID\"", 
                 proj
            );
            Assertion.AssertEquals(SolutionProjectType.Unknown, proj.ProjectType);
            Assertion.AssertEquals("MyProject,(=IsGreat)", proj.ProjectName);
            Assertion.AssertEquals("Relative path to project file", proj.RelativePath);
            Assertion.AssertEquals("Unique name-GUID", proj.ProjectGuid);
        }   

        /// <summary>
        /// Helper method to create a SolutionParser object, and call it to parse the SLN file
        /// represented by the string contents passed in.
        /// </summary>
        /// <param name="solutionFileContents"></param>
        /// <returns></returns>
        static internal SolutionParser ParseSolutionHelper
            (
            string solutionFileContents
            )
        {
            solutionFileContents = solutionFileContents.Replace('\'', '"');
            StreamReader sr = StreamHelpers.StringToStreamReader(solutionFileContents);

            SolutionParser sp = new SolutionParser();
            sp.SolutionFileDirectory = Path.GetTempPath();
            sp.SolutionReader = sr;
            string tmpFileName = Path.GetTempFileName();
            sp.SolutionFile = tmpFileName + ".sln";
            // This file is not expected to exist at this point, so make sure it doesn't
            File.Delete(sp.SolutionFile);
            sp.ParseSolution();
            // Clean up the temporary file that got created with this call
            File.Delete(tmpFileName);
            return sp;
        }

        /// <summary>
        /// Ensure that a bogus version stamp in the .SLN file results in an
        /// InvalidProjectFileException.
        /// </summary>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void BadVersionStamp()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version a.b
                # Visual Studio 2005
                ";

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
        }
        
        /// <summary>
        /// Expected version numbers less than 7 to cause an invalid project file exception.
        /// </summary>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void VersionTooLow()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 6.0
                # Visual Studio 2005
                ";

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
        }

        /// <summary>
        /// Ensure that an unsupported version greater than the current maximum (10) in the .SLN file results in a
        /// comment indicating we will try and continue
        /// </summary>
        [Test]
        public void UnsupportedVersion()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 999.0
                # Visual Studio 2005
                ";

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
            Assert.IsTrue(solution.SolutionParserComments.Count == 1, "Expected the solution parser to contain one comment");
            Assert.IsTrue(String.Equals((string)solution.SolutionParserComments[0], ResourceUtilities.FormatResourceString("UnrecognizedSolutionComment", "999"), StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void Version9()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.0
                # Visual Studio 2005
                ";

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
            
            Assertion.AssertEquals(9, solution.Version);
        }

        [Test]
        public void Version10()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 10.0
                # Visual Studio 2005
                ";

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(10, solution.Version);
        }

        /// <summary>
        /// Test to parse a very basic .sln file to validate that description property in a solution file 
        /// is properly handled.
        /// </summary>
        /// <owner>yroy</owner>
        [Test]
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
                SolutionParser solution = ParseSolutionHelper(solutionFileContents);
            }
            catch(Exception ex)
            {
                Assertion.Assert("Failed to parse solution containing description information. Error: " + ex.Message, false);
            }
        }

        /// <summary>
        /// Tests the parsing of a very basic .SLN file with three independent projects.
        /// </summary>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(3, solution.Projects.Length);
            
            Assertion.AssertEquals(SolutionProjectType.ManagedProject,  solution.Projects[0].ProjectType);
            Assertion.AssertEquals("ConsoleApplication1",                      solution.Projects[0].ProjectName);
            Assertion.AssertEquals(@"ConsoleApplication1\ConsoleApplication1.vbproj", solution.Projects[0].RelativePath);
            Assertion.AssertEquals("{AB3413A6-D689-486D-B7F0-A095371B3F13}",   solution.Projects[0].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[0].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[0].ParentProjectGuid);
            Assertion.AssertEquals("ConsoleApplication1",                      solution.Projects[0].GetUniqueProjectName());

            Assertion.AssertEquals(SolutionProjectType.ManagedProject,  solution.Projects[1].ProjectType);
            Assertion.AssertEquals("vbClassLibrary",                           solution.Projects[1].ProjectName);
            Assertion.AssertEquals(@"vbClassLibrary\vbClassLibrary.vbproj",    solution.Projects[1].RelativePath);
            Assertion.AssertEquals("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}",   solution.Projects[1].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[1].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[1].ParentProjectGuid);
            Assertion.AssertEquals("vbClassLibrary",                           solution.Projects[1].GetUniqueProjectName());

            Assertion.AssertEquals(SolutionProjectType.ManagedProject,  solution.Projects[2].ProjectType);
            Assertion.AssertEquals("ClassLibrary1",                            solution.Projects[2].ProjectName);
            Assertion.AssertEquals(@"ClassLibrary1\ClassLibrary1.csproj",      solution.Projects[2].RelativePath);
            Assertion.AssertEquals("{DEBCE986-61B9-435E-8018-44B9EF751655}",   solution.Projects[2].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[2].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[2].ParentProjectGuid);
            Assertion.AssertEquals("ClassLibrary1",                            solution.Projects[2].GetUniqueProjectName());

        }   

        /// <summary>
        /// Exercises solution folders, and makes sure that samely named projects in different
        /// solution folders will get correctly uniquified.
        /// </summary>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(5, solution.Projects.Length);
            
            Assertion.AssertEquals(@"ClassLibrary1\ClassLibrary1.csproj",      solution.Projects[0].RelativePath);
            Assertion.AssertEquals("{34E0D07D-CF8F-459D-9449-C4188D8C5564}",   solution.Projects[0].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[0].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[0].ParentProjectGuid);
            Assertion.AssertEquals("ClassLibrary1",                            solution.Projects[0].GetUniqueProjectName());

            Assertion.AssertEquals(SolutionProjectType.SolutionFolder,  solution.Projects[1].ProjectType);
            Assertion.AssertEquals("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}",   solution.Projects[1].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[1].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[1].ParentProjectGuid);
            Assertion.AssertEquals("MySlnFolder",                              solution.Projects[1].GetUniqueProjectName());

            Assertion.AssertEquals(@"MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj", solution.Projects[2].RelativePath);
            Assertion.AssertEquals("{A5EE8128-B08E-4533-86C5-E46714981680}",   solution.Projects[2].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[2].Dependencies.Count);
            Assertion.AssertEquals("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}",   solution.Projects[2].ParentProjectGuid);
            Assertion.AssertEquals(@"MySlnFolder\ClassLibrary1",               solution.Projects[2].GetUniqueProjectName());

            Assertion.AssertEquals(SolutionProjectType.SolutionFolder,  solution.Projects[3].ProjectType);
            Assertion.AssertEquals("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}",   solution.Projects[3].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[3].Dependencies.Count);
            Assertion.AssertEquals("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}",   solution.Projects[3].ParentProjectGuid);
            Assertion.AssertEquals(@"MySlnFolder\MySubSlnFolder",              solution.Projects[3].GetUniqueProjectName());

            Assertion.AssertEquals(@"ClassLibrary2\ClassLibrary2.csproj",      solution.Projects[4].RelativePath);
            Assertion.AssertEquals("{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}",   solution.Projects[4].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[4].Dependencies.Count);
            Assertion.AssertEquals("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}",   solution.Projects[4].ParentProjectGuid);
            Assertion.AssertEquals(@"MySlnFolder\MySubSlnFolder\ClassLibrary2",solution.Projects[4].GetUniqueProjectName());
        }

        /// <summary>
        /// Verifies that hand-coded project-to-project dependencies listed in the .SLN file
        /// are correctly recognized by our solution parser.
        /// </summary>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(3, solution.Projects.Length);
            
            Assertion.AssertEquals(@"ClassLibrary1\ClassLibrary1.csproj",      solution.Projects[0].RelativePath);
            Assertion.AssertEquals("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}",   solution.Projects[0].ProjectGuid);
            Assertion.AssertEquals(1,                                          solution.Projects[0].Dependencies.Count);
            Assertion.AssertEquals("{FAB4EE06-6E01-495A-8926-5514599E3DD9}",   (string) solution.Projects[0].Dependencies[0]);
            Assertion.AssertEquals(null,                                       solution.Projects[0].ParentProjectGuid);
            Assertion.AssertEquals("ClassLibrary1",                            solution.Projects[0].GetUniqueProjectName());

            Assertion.AssertEquals(@"ClassLibrary2\ClassLibrary2.csproj",      solution.Projects[1].RelativePath);
            Assertion.AssertEquals("{7F316407-AE3E-4F26-BE61-2C50D30DA158}",   solution.Projects[1].ProjectGuid);
            Assertion.AssertEquals(2,                                          solution.Projects[1].Dependencies.Count);
            Assertion.AssertEquals("{FAB4EE06-6E01-495A-8926-5514599E3DD9}",   (string) solution.Projects[1].Dependencies[0]);
            Assertion.AssertEquals("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}",   (string) solution.Projects[1].Dependencies[1]);
            Assertion.AssertEquals(null,                                       solution.Projects[1].ParentProjectGuid);
            Assertion.AssertEquals("ClassLibrary2",                            solution.Projects[1].GetUniqueProjectName());

            Assertion.AssertEquals(@"ClassLibrary3\ClassLibrary3.csproj",      solution.Projects[2].RelativePath);
            Assertion.AssertEquals("{FAB4EE06-6E01-495A-8926-5514599E3DD9}",   solution.Projects[2].ProjectGuid);
            Assertion.AssertEquals(0,                                          solution.Projects[2].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[2].ParentProjectGuid);
            Assertion.AssertEquals("ClassLibrary3",                            solution.Projects[2].GetUniqueProjectName());
        }

        /// <summary>
        /// Tests to see that all the data/properties are correctly parsed out of a Venus
        /// project in a .SLN.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents.Replace('`', '"'));

            Assertion.AssertEquals(1, solution.Projects.Length);
            
            Assertion.AssertEquals(SolutionProjectType.WebProject,      solution.Projects[0].ProjectType);
            Assertion.AssertEquals(@"C:\WebSites\WebApplication3\",            solution.Projects[0].ProjectName);
            Assertion.AssertEquals(@"C:\WebSites\WebApplication3\",            solution.Projects[0].RelativePath);
            Assertion.AssertEquals("{464FD0B9-E335-4677-BE1E-6B2F982F4D86}",   solution.Projects[0].ProjectGuid);
            Assertion.AssertEquals(2,                                          solution.Projects[0].Dependencies.Count);
            Assertion.AssertEquals(null,                                       solution.Projects[0].ParentProjectGuid);
            Assertion.AssertEquals(@"C:\WebSites\WebApplication3\",            solution.Projects[0].GetUniqueProjectName());

            Hashtable aspNetCompilerParameters = solution.Projects[0].AspNetConfigurations;
            AspNetCompilerParameters debugAspNetCompilerParameters = (AspNetCompilerParameters) aspNetCompilerParameters["Debug"];
            AspNetCompilerParameters releaseAspNetCompilerParameters = (AspNetCompilerParameters) aspNetCompilerParameters["Release"];

            Assertion.AssertEquals(@"/publishfirst",                           debugAspNetCompilerParameters.aspNetVirtualPath);
            Assertion.AssertEquals(@"..\rajeev\temp\websites\myfirstwebsite\", debugAspNetCompilerParameters.aspNetPhysicalPath);
            Assertion.AssertEquals(@"..\rajeev\temp\publishfirst\",            debugAspNetCompilerParameters.aspNetTargetPath);
            Assertion.AssertEquals(@"true",                                    debugAspNetCompilerParameters.aspNetForce);
            Assertion.AssertEquals(@"false",                                   debugAspNetCompilerParameters.aspNetUpdateable);
            Assertion.AssertEquals(@"true",                                    debugAspNetCompilerParameters.aspNetDebug);
            Assertion.AssertEquals(@"debugkeyfile.snk",                        debugAspNetCompilerParameters.aspNetKeyFile);
            Assertion.AssertEquals(@"12345.container",                         debugAspNetCompilerParameters.aspNetKeyContainer);
            Assertion.AssertEquals(@"true",                                    debugAspNetCompilerParameters.aspNetDelaySign);
            Assertion.AssertEquals(@"false",                                   debugAspNetCompilerParameters.aspNetAPTCA);
            Assertion.AssertEquals(@"debugfixednames",                         debugAspNetCompilerParameters.aspNetFixedNames);

            Assertion.AssertEquals(@"/publishfirst_release",                           releaseAspNetCompilerParameters.aspNetVirtualPath);
            Assertion.AssertEquals(@"..\rajeev\temp\websites\myfirstwebsite_release\", releaseAspNetCompilerParameters.aspNetPhysicalPath);
            Assertion.AssertEquals(@"..\rajeev\temp\publishfirst_release\",            releaseAspNetCompilerParameters.aspNetTargetPath);
            Assertion.AssertEquals(@"true",                                            releaseAspNetCompilerParameters.aspNetForce);
            Assertion.AssertEquals(@"true",                                            releaseAspNetCompilerParameters.aspNetUpdateable);
            Assertion.AssertEquals(@"false",                                           releaseAspNetCompilerParameters.aspNetDebug);
            Assertion.AssertEquals("",                                                 releaseAspNetCompilerParameters.aspNetKeyFile);
            Assertion.AssertEquals("",                                                 releaseAspNetCompilerParameters.aspNetKeyContainer);
            Assertion.AssertEquals("",                                                 releaseAspNetCompilerParameters.aspNetDelaySign);
            Assertion.AssertEquals("",                                                 releaseAspNetCompilerParameters.aspNetAPTCA);
            Assertion.AssertEquals("",                                                 releaseAspNetCompilerParameters.aspNetFixedNames);

            ArrayList aspNetProjectReferences = solution.Projects[0].ProjectReferences;
            Assertion.AssertEquals(2, aspNetProjectReferences.Count);
            Assertion.AssertEquals("{FD705688-88D1-4C22-9BFF-86235D89C2FC}", aspNetProjectReferences[0]);
            Assertion.AssertEquals("{F0726D09-042B-4A7A-8A01-6BED2422BD5D}", aspNetProjectReferences[1]);
        }

        /// <summary>
        /// Tests to see that our solution parser correctly recognizes a Venus project that
        /// sits inside a solution folder.
        /// </summary>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(3, solution.Projects.Length);
            
            Assertion.AssertEquals(SolutionProjectType.WebProject,      solution.Projects[0].ProjectType);
            Assertion.AssertEquals(@"C:\WebSites\WebApplication3\",            solution.Projects[0].GetUniqueProjectName());

            Assertion.AssertEquals(SolutionProjectType.SolutionFolder,  solution.Projects[1].ProjectType);
            Assertion.AssertEquals("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}",   solution.Projects[1].ProjectGuid);

            Assertion.AssertEquals(SolutionProjectType.WebProject,      solution.Projects[2].ProjectType);
            Assertion.AssertEquals(@"C:\WebSites\WebApplication4\",            solution.Projects[2].GetUniqueProjectName());
            Assertion.AssertEquals("{092FE6E5-71F8-43F7-9C92-30E3124B8A22}",   solution.Projects[2].ParentProjectGuid);
        }

        /// <summary>
        /// Make sure the solution configurations get parsed correctly for a simple mixed C#/VC solution
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void ParseSolutionConfigurations()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            Assertion.AssertEquals(6, solution.SolutionConfigurations.Count);

            List<string> configurationNames = new List<string>(6);
            foreach (ConfigurationInSolution configuration in solution.SolutionConfigurations)
            {
                configurationNames.Add(configuration.FullName);
            }

            Assertion.Assert(configurationNames.Contains("Debug|Any CPU"));
            Assertion.Assert(configurationNames.Contains("Debug|Mixed Platforms"));
            Assertion.Assert(configurationNames.Contains("Debug|Win32"));
            Assertion.Assert(configurationNames.Contains("Release|Any CPU"));
            Assertion.Assert(configurationNames.Contains("Release|Mixed Platforms"));
            Assertion.Assert(configurationNames.Contains("Release|Win32"));

            Assertion.AssertEquals("Default solution configuration", "Debug", solution.GetDefaultConfigurationName());
            Assertion.AssertEquals("Default solution platform", "Mixed Platforms", solution.GetDefaultPlatformName());
        }

        /// <summary>
        /// Test some invalid cases for solution configuration parsing.
        /// There can be only one '=' character in a sln cfg entry, separating two identical names
        /// </summary>
        /// <owner>LukaszG</owner>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void ParseInvalidSolutionConfigurations1()
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
        }

        /// <summary>
        /// Test some invalid cases for solution configuration parsing
        /// There can be only one '=' character in a sln cfg entry, separating two identical names
        /// </summary>
        /// <owner>LukaszG</owner>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void ParseInvalidSolutionConfigurations2()
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
        }

        /// <summary>
        /// Test some invalid cases for solution configuration parsing
        /// Solution configurations must include the platform part
        /// </summary>
        /// <owner>LukaszG</owner>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void ParseInvalidSolutionConfigurations3()
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);
        }

        /// <summary>
        /// Make sure the project configurations in solution configurations get parsed correctly 
        /// for a simple mixed C#/VC solution
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void ParseProjectConfigurationsInSolutionConfigurations1()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution csProject = (ProjectInSolution) solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution vcProject = (ProjectInSolution) solution.ProjectsByGuid["{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"];

            Assertion.AssertEquals(6, csProject.ProjectConfigurations.Count);

            Assertion.AssertEquals("Debug|AnyCPU", csProject.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assertion.AssertEquals(true, csProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assertion.AssertEquals("Release|AnyCPU", csProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName);
            Assertion.AssertEquals(true, csProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild);

            Assertion.AssertEquals("Debug|AnyCPU", csProject.ProjectConfigurations["Debug|Win32"].FullName);
            Assertion.AssertEquals(false, csProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assertion.AssertEquals("Release|AnyCPU", csProject.ProjectConfigurations["Release|Any CPU"].FullName);
            Assertion.AssertEquals(true, csProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);

            Assertion.AssertEquals("Release|AnyCPU", csProject.ProjectConfigurations["Release|Mixed Platforms"].FullName);
            Assertion.AssertEquals(true, csProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild);

            Assertion.AssertEquals("Release|AnyCPU", csProject.ProjectConfigurations["Release|Win32"].FullName);
            Assertion.AssertEquals(false, csProject.ProjectConfigurations["Release|Win32"].IncludeInBuild);

            Assertion.AssertEquals(6, vcProject.ProjectConfigurations.Count);

            Assertion.AssertEquals("Debug|Win32", vcProject.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assertion.AssertEquals(false, vcProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assertion.AssertEquals("Debug|Win32", vcProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName);
            Assertion.AssertEquals(true, vcProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild);

            Assertion.AssertEquals("Debug|Win32", vcProject.ProjectConfigurations["Debug|Win32"].FullName);
            Assertion.AssertEquals(true, vcProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assertion.AssertEquals("Release|Win32", vcProject.ProjectConfigurations["Release|Any CPU"].FullName);
            Assertion.AssertEquals(false, vcProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);

            Assertion.AssertEquals("Release|Win32", vcProject.ProjectConfigurations["Release|Mixed Platforms"].FullName);
            Assertion.AssertEquals(true, vcProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild);

            Assertion.AssertEquals("Release|Win32", vcProject.ProjectConfigurations["Release|Win32"].FullName);
            Assertion.AssertEquals(true, vcProject.ProjectConfigurations["Release|Win32"].IncludeInBuild);
        }

        /// <summary>
        /// Make sure the project configurations in solution configurations get parsed correctly 
        /// for a more tricky solution
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
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

            SolutionParser solution = ParseSolutionHelper(solutionFileContents);

            ProjectInSolution webProject = (ProjectInSolution)solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F418}"];
            ProjectInSolution exeProject = (ProjectInSolution)solution.ProjectsByGuid["{25FD9E7C-F37E-48E0-9A7C-607FE4AACCC0}"];
            ProjectInSolution missingWebProject = (ProjectInSolution)solution.ProjectsByGuid["{E8E75132-67E4-4D6F-9CAE-8DA4C883F419}"];

            Assertion.AssertEquals(1, webProject.ProjectConfigurations.Count);

            Assertion.AssertEquals("Debug|.NET", webProject.ProjectConfigurations["Debug|.NET"].FullName);
            Assertion.AssertEquals(true, webProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild);

            Assertion.AssertEquals(1, exeProject.ProjectConfigurations.Count);

            Assertion.AssertEquals("Debug", exeProject.ProjectConfigurations["Debug|.NET"].FullName);
            Assertion.AssertEquals(false, exeProject.ProjectConfigurations["Debug|.NET"].IncludeInBuild);

            Assertion.AssertEquals(0, missingWebProject.ProjectConfigurations.Count);

            Assertion.AssertEquals("Default solution configuration", "Debug", solution.GetDefaultConfigurationName());
            Assertion.AssertEquals("Default solution platform", ".NET", solution.GetDefaultPlatformName());
        }
    }
}
