// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;
using System.Text;

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the parts of SolutionFile that are surfaced as public API
    /// </summary>
    public class SolutionFile_Tests
    {
        [Theory]
        [InlineData(@"
                {
                  ""solution"": {
                    ""path"": ""C:\\notAPath\\MSBuild.Dev.sln"",
                    ""projects2"": [
                      ""src\\Build\\Microsoft.Build.csproj"",
                      ""src\\Framework\\Microsoft.Build.Framework.csproj"",
                      ""src\\MSBuild\\MSBuild.csproj"",
                      ""src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj""
                    ]
                    }
                }
                ", "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(@"
                [{
                  ""solution"": {
                    ""path"": ""C:\\notAPath\\MSBuild.Dev.sln"",
                    ""projects"": [
                      ""src\\Build\\Microsoft.Build.csproj"",
                      ""src\\Framework\\Microsoft.Build.Framework.csproj"",
                      ""src\\MSBuild\\MSBuild.csproj"",
                      ""src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj""
                    ]
                    }
                }]
                ", "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(@"
                {
                  ""solution"": {
                    ""path"": ""C:\\notAPath\\MSBuild.Dev.sln"",
                    ""projects"": [
                      {""path"": ""src\\Build\\Microsoft.Build.csproj""},
                      {""path"": ""src\\Framework\\Microsoft.Build.Framework.csproj""},
                      {""path"": ""src\\MSBuild\\MSBuild.csproj""},
                      {""path"": ""src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj""}
                    ]
                    }
                }
                ", "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(@"
                {
                  ""solution"": {
                    ""path"": ""C:\\notAPath2\\MSBuild.Dev.sln"",
                    ""projects"": [
                      {""path"": ""src\\Build\\Microsoft.Build.csproj""},
                      {""path"": ""src\\Framework\\Microsoft.Build.Framework.csproj""},
                      {""path"": ""src\\MSBuild\\MSBuild.csproj""},
                      {""path"": ""src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj""}
                    ]
                    }
                }
                ", "MSBuild.SolutionFilterMissingSolutionError")]
        public void InvalidSolutionFilters(string slnfValue, string exceptionReason)
        {
            Assert.False(File.Exists("C:\\notAPath2\\MSBuild.Dev.sln"));
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFile sln = testEnvironment.CreateFile(folder, "Dev.sln");
                TransientTestFile slnf = testEnvironment.CreateFile(folder, "Dev.slnf", slnfValue.Replace(@"C:\\notAPath\\MSBuild.Dev.sln", sln.Path.Replace("\\", "\\\\")));
                InvalidProjectFileException e = Should.Throw<InvalidProjectFileException>(() => SolutionFile.Parse(slnf.Path));
                e.HelpKeyword.ShouldBe(exceptionReason);
            }
        }

        /// <summary>
        /// Test that a project with the C++ project guid and an extension of vcproj is seen as invalid.
        /// </summary>
        [Fact]
        public void ParseSolution_VC()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFileContents =
                    @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'Project name.vcproj', 'Relative path\to\Project name.vcproj', '{0ABED153-9451-483C-8140-9E8D7306B216}'
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

                ParseSolutionHelper(solutionFileContents);
                Assert.True(false, "Should not get here");
            }
           );
        }
        /// <summary>
        /// Test that a project with the C++ project guid and an arbitrary extension is seen as valid -- 
        /// we assume that all C++ projects except .vcproj are MSBuild format. 
        /// </summary>
        [Fact]
        public void ParseSolution_VC2()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'Project name.myvctype', 'Relative path\to\Project name.myvctype', '{0ABED153-9451-483C-8140-9E8D7306B216}'
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

            Assert.Equal("Project name.myvctype", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal("Relative path\\to\\Project name.myvctype", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
        }

        /// <summary>
        /// A slightly more complicated test where there is some different whitespace.
        /// </summary>
        [Fact]
        public void ParseSolutionWithDifferentSpacing()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project(' { Project GUID} ')  = ' Project name ',  ' Relative path to project file '    , ' {0ABED153-9451-483C-8140-9E8D7306B216} '
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

            Assert.Equal("Project name", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal("Relative path to project file", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
        }

        /// <summary>
        /// Solution with an empty project name.  This is somewhat malformed, but we should
        /// still behave reasonably instead of crashing.
        /// </summary>
        [Fact]
        public void ParseSolution_EmptyProjectName()
        {
            string solutionFileContents =
                           @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{Project GUID}') = '', 'src\.proj', '{0ABED153-9451-483C-8140-9E8D7306B216}'
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

            Assert.StartsWith("EmptyProjectName", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal("src\\.proj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
        }

        /// <summary>
        /// Test some characters that are valid in a file name but that also could be
        /// considered a delimiter by a parser. Does quoting work for special characters?
        /// </summary>
        [Fact]
        public void ParseSolutionWhereProjectNameHasSpecialCharacters()
        {
            string solutionFileContents =
                           @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{Project GUID}')  = 'MyProject,(=IsGreat)',  'Relative path to project file'    , '{0ABED153-9451-483C-8140-9E8D7306B216}'
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

            Assert.Equal("MyProject,(=IsGreat)", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal("Relative path to project file", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
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

            Assert.Equal("ConsoleApplication1", solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal(@"ConsoleApplication1\ConsoleApplication1.vbproj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{AB3413A6-D689-486D-B7F0-A095371B3F13}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[0].Dependencies);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);

            Assert.Equal("vbClassLibrary", solution.ProjectsInOrder[1].ProjectName);
            Assert.Equal(@"vbClassLibrary\vbClassLibrary.vbproj", solution.ProjectsInOrder[1].RelativePath);
            Assert.Equal("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[1].Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);

            Assert.Equal("ClassLibrary1", solution.ProjectsInOrder[2].ProjectName);
            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{DEBCE986-61B9-435E-8018-44B9EF751655}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);
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

            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[1].Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);

            Assert.Equal(@"MyPhysicalFolder\ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{A5EE8128-B08E-4533-86C5-E46714981680}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[2].ParentProjectGuid);

            Assert.Equal("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", solution.ProjectsInOrder[3].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[3].Dependencies);
            Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", solution.ProjectsInOrder[3].ParentProjectGuid);

            Assert.Equal(@"ClassLibrary2\ClassLibrary2.csproj", solution.ProjectsInOrder[4].RelativePath);
            Assert.Equal("{6DB98C35-FDCC-4818-B5D4-1F0A385FDFD4}", solution.ProjectsInOrder[4].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[4].Dependencies);
            Assert.Equal("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", solution.ProjectsInOrder[4].ParentProjectGuid);
        }

        /// <summary>
        /// Exercises shared projects.
        /// </summary>
        [Fact]
        public void SharedProjects()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 15
                VisualStudioVersion = 15.0.27610.1
                MinimumVisualStudioVersion = 10.0.40219.1
                Project('{D954291E-2A0B-460D-934E-DC6B0785DB48}') = 'SharedProject1', 'SharedProject1\SharedProject1.shproj', '{14686F51-D0C2-4832-BBAA-6FBAEC676995}'
                EndProject
                Project('{D954291E-2A0B-460D-934E-DC6B0785DB48}') = 'SharedProject2', 'SharedProject2\SharedProject2.shproj', '{BAE750E8-4656-4947-B06B-3961E1051DF7}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{3A0EC360-A42A-417F-BDEF-619682CF6119}'
                EndProject
                Project('{F184B08F-C81C-45F6-A57F-5ABD9991F28F}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.vbproj', '{6DEF6DE8-FBF0-4240-B469-282DEE87899C}'
                EndProject
                Global
                    GlobalSection(SharedMSBuildProjectFiles) = preSolution
                        SharedProject1\SharedProject1.projitems*{14686f51-d0c2-4832-bbaa-6fbaec676995}*SharedItemsImports = 13
                        SharedProject1\SharedProject1.projitems*{3a0ec360-a42a-417f-bdef-619682cf6119}*SharedItemsImports = 4
                        SharedProject2\SharedProject2.projitems*{6def6de8-fbf0-4240-b469-282dee87899c}*SharedItemsImports = 4
                        SharedProject2\SharedProject2.projitems*{bae750e8-4656-4947-b06b-3961e1051df7}*SharedItemsImports = 13
                    EndGlobalSection
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {3A0EC360-A42A-417F-BDEF-619682CF6119}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {3A0EC360-A42A-417F-BDEF-619682CF6119}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {3A0EC360-A42A-417F-BDEF-619682CF6119}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {3A0EC360-A42A-417F-BDEF-619682CF6119}.Release|Any CPU.Build.0 = Release|Any CPU
                        {6DEF6DE8-FBF0-4240-B469-282DEE87899C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {6DEF6DE8-FBF0-4240-B469-282DEE87899C}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {6DEF6DE8-FBF0-4240-B469-282DEE87899C}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {6DEF6DE8-FBF0-4240-B469-282DEE87899C}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(ExtensibilityGlobals) = postSolution
                        SolutionGuid = {1B671EF6-A62A-4497-8351-3EE8679CA86F}
                    EndGlobalSection
                EndGlobal
                ";

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            Assert.Equal(4, solution.ProjectsInOrder.Count);

            Assert.Equal(@"SharedProject1\SharedProject1.shproj", solution.ProjectsInOrder[0].RelativePath);
            Assert.Equal("{14686F51-D0C2-4832-BBAA-6FBAEC676995}", solution.ProjectsInOrder[0].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[0].Dependencies);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);

            Assert.Equal(@"SharedProject2\SharedProject2.shproj", solution.ProjectsInOrder[1].RelativePath);
            Assert.Equal("{BAE750E8-4656-4947-B06B-3961E1051DF7}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[1].Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);

            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{3A0EC360-A42A-417F-BDEF-619682CF6119}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);

            Assert.Equal(@"ClassLibrary2\ClassLibrary2.vbproj", solution.ProjectsInOrder[3].RelativePath);
            Assert.Equal("{6DEF6DE8-FBF0-4240-B469-282DEE87899C}", solution.ProjectsInOrder[3].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[3].Dependencies);
            Assert.Null(solution.ProjectsInOrder[3].ParentProjectGuid);
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

            Assert.Equal(@"ClassLibrary2\ClassLibrary2.csproj", solution.ProjectsInOrder[1].RelativePath);
            Assert.Equal("{7F316407-AE3E-4F26-BE61-2C50D30DA158}", solution.ProjectsInOrder[1].ProjectGuid);
            Assert.Equal(2, solution.ProjectsInOrder[1].Dependencies.Count);
            Assert.Equal("{FAB4EE06-6E01-495A-8926-5514599E3DD9}", (string)solution.ProjectsInOrder[1].Dependencies[0]);
            Assert.Equal("{05A5AD00-71B5-4612-AF2F-9EA9121C4111}", (string)solution.ProjectsInOrder[1].Dependencies[1]);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);

            Assert.Equal(@"ClassLibrary3\ClassLibrary3.csproj", solution.ProjectsInOrder[2].RelativePath);
            Assert.Equal("{FAB4EE06-6E01-495A-8926-5514599E3DD9}", solution.ProjectsInOrder[2].ProjectGuid);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);
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

            ProjectInSolution csharpProject = (ProjectInSolution)solution.ProjectsByGuid["{6185CC21-BE89-448A-B3C0-D1C27112E595}"];
            ProjectInSolution vcProject = (ProjectInSolution)solution.ProjectsByGuid["{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"];

            Assert.Equal(6, csharpProject.ProjectConfigurations.Count);

            Assert.Equal("Debug|AnyCPU", csharpProject.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assert.True(csharpProject.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csharpProject.ProjectConfigurations["Debug|Mixed Platforms"].FullName);
            Assert.True(csharpProject.ProjectConfigurations["Debug|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Debug|AnyCPU", csharpProject.ProjectConfigurations["Debug|Win32"].FullName);
            Assert.False(csharpProject.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csharpProject.ProjectConfigurations["Release|Any CPU"].FullName);
            Assert.True(csharpProject.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csharpProject.ProjectConfigurations["Release|Mixed Platforms"].FullName);
            Assert.True(csharpProject.ProjectConfigurations["Release|Mixed Platforms"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", csharpProject.ProjectConfigurations["Release|Win32"].FullName);
            Assert.False(csharpProject.ProjectConfigurations["Release|Win32"].IncludeInBuild);

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

        /// <summary>
        /// Parse solution file with comments
        /// </summary>
        [Fact]
        public void ParseSolutionWithComments()
        {
            const string solutionFileContent = @"
                    Microsoft Visual Studio Solution File, Format Version 12.00
                    # Visual Studio Version 16
                    VisualStudioVersion = 16.0.29123.89
                    MinimumVisualStudioVersion = 10.0.40219.1
                    Project('{9A19103F-16F7-4668-BE54-9A1E7A4F7556}') = 'SlnCommentTest', 'SlnCommentTest.csproj', '{00000000-0000-0000-FFFF-FFFFFFFFFFFF}'
                    EndProject
                    Project('{2150E333-8FDC-42A3-9474-1A3956D46DE8}') = 'Solution Items', 'Solution Items', '{054DED3B-B890-4652-B449-839F581E5D86}'
	                    ProjectSection(SolutionItems) = preProject
		                    SlnFile.txt = SlnFile.txt
	                    EndProjectSection
                    EndProject
                    Global
	                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
		                    Debug|Any CPU = Debug|Any CPU
		                    Release|Any CPU = Release|Any CPU
	                    EndGlobalSection
	                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
		                    {00000000-0000-0000-FFFF-FFFFFFFFFFFF}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		                    {00000000-0000-0000-FFFF-FFFFFFFFFFFF}.Debug|Any CPU.Build.0 = Debug|Any CPU
		                    {00000000-0000-0000-FFFF-FFFFFFFFFFFF}.Release|Any CPU.ActiveCfg = Release|Any CPU
		                    {00000000-0000-0000-FFFF-FFFFFFFFFFFF}.Release|Any CPU.Build.0 = Release|Any CPU
	                    EndGlobalSection
	                    GlobalSection(SolutionProperties) = preSolution
		                    HideSolutionNode = FALSE
	                    EndGlobalSection
	                    GlobalSection(ExtensibilityGlobals) = postSolution
		                    SolutionGuid = {FFFFFFFF-FFFF-FFFF-0000-000000000000}
	                    EndGlobalSection
                    EndGlobal
                    ";

            StringBuilder stringBuilder = new StringBuilder();

            // Put comment between all lines
            const string comment = "\t# comment";
            string[] lines = solutionFileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                stringBuilder.AppendLine(comment);
                stringBuilder.AppendLine(lines[i]);
            }
            stringBuilder.AppendLine(comment);

            Should.NotThrow(() => ParseSolutionHelper(stringBuilder.ToString()));
        }

        /// <summary>
        /// Helper method to create a SolutionFile object, and call it to parse the SLN file
        /// represented by the string contents passed in.
        /// </summary>
        private static SolutionFile ParseSolutionHelper(string solutionFileContents)
        {
            solutionFileContents = solutionFileContents.Replace('\'', '"');
            string solutionPath = FileUtilities.GetTemporaryFile(".sln");

            try
            {
                File.WriteAllText(solutionPath, solutionFileContents);
                SolutionFile sp = SolutionFile.Parse(solutionPath);
                return sp;
            }
            finally
            {
                File.Delete(solutionPath);
            }
        }
    }
}
