// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the parts of SolutionFile that are surfaced as public API
    /// </summary>
    public class SolutionFile_Tests
    {
        /// <summary>
        /// Test that a project with the C++ project guid and an extension of vcproj is seen as invalid.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ParseSolution_VC(bool isOptInSlnParsingWithNewParser)
        {
            string solutionFileContents =
            """
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
            EndGlobalf
            """;

            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser);
                Assert.Fail("Should not get here");
            });
        }

        /// <summary>
        /// Test that a project with the C++ project guid and an arbitrary extension is seen as valid --
        /// we assume that all C++ projects except .vcproj are MSBuild format.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ParseSolution_VC2(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            string expectedProjectName = convertToSlnx ? "Project name" : "Project name.myvctype";
            Assert.Equal(expectedProjectName, solution.ProjectsInOrder[0].ProjectName);
            Assert.Equal(ConvertToUnixPathIfNeeded("Relative path\\to\\Project name.myvctype", convertToSlnx || isOptInSlnParsingWithNewParser), solution.ProjectsInOrder[0].RelativePath);
            if (!convertToSlnx)
            {
                // When converting to SLNX, the project GUID is not preserved.
                Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
            }
        }

        /// <summary>
        /// Solution with an empty project name.  
        // This is somewhat malformed, but with old parser we should still behave reasonably instead of crashing.
        // The new parser throws an exception.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ParseSolution_EmptyProjectName(bool isOptInSlnParsingWithNewParser)
        {
            string solutionFileContents =
            """
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
            """;

            if (isOptInSlnParsingWithNewParser)
            {
                Assert.Throws<InvalidProjectFileException>(() =>
                {
                    SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser);
                });
            }
            else
            {
                SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser);
                Assert.StartsWith("EmptyProjectName", solution.ProjectsInOrder[0].ProjectName);
                Assert.Equal("src\\.proj", solution.ProjectsInOrder[0].RelativePath);
                Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.ProjectsInOrder[0].ProjectGuid);
            }
        }

        /// <summary>
        /// Tests the parsing of a very basic .SLN file with three independent projects.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void BasicSolution(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            // When converting to slnx, the order of the projects is not preserved.
            bool usesNewParser = convertToSlnx || isOptInSlnParsingWithNewParser;
            ProjectInSolution consoleApplication1 = solution.ProjectsInOrder.First(p => p.ProjectName == "ConsoleApplication1");
            Assert.Equal(ConvertToUnixPathIfNeeded("ConsoleApplication1\\ConsoleApplication1.vbproj", usesNewParser), consoleApplication1.RelativePath);
            Assert.Empty(consoleApplication1.Dependencies);
            Assert.Null(consoleApplication1.ParentProjectGuid);

            ProjectInSolution vbClassLibrary = solution.ProjectsInOrder.First(p => p.ProjectName == "vbClassLibrary");
            Assert.Equal(ConvertToUnixPathIfNeeded("vbClassLibrary\\vbClassLibrary.vbproj", usesNewParser), vbClassLibrary.RelativePath);
            Assert.Empty(vbClassLibrary.Dependencies);
            Assert.Null(vbClassLibrary.ParentProjectGuid);

            ProjectInSolution classLibrary1 = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary1");
            Assert.Equal(ConvertToUnixPathIfNeeded("ClassLibrary1\\ClassLibrary1.csproj", usesNewParser), classLibrary1.RelativePath);
            Assert.Empty(classLibrary1.Dependencies);
            Assert.Null(classLibrary1.ParentProjectGuid);

            if (!convertToSlnx)
            {
                Assert.Equal("{AB3413A6-D689-486D-B7F0-A095371B3F13}", consoleApplication1.ProjectGuid);
                Assert.Equal("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}", vbClassLibrary.ProjectGuid);
                Assert.Equal("{DEBCE986-61B9-435E-8018-44B9EF751655}", classLibrary1.ProjectGuid);
            }
        }

        /// <summary>
        /// Exercises solution folders, and makes sure that samely named projects in different
        /// solution folders will get correctly uniquified.
        /// For the new parser, solution folders are not included to ProjectsInOrder or ProjectsByGuid.
        /// See the test with the same name in SolutionFile_Tests_OldParser.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SolutionFolders(bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            bool isOptInSlnParsingWithNewParser = !convertToSlnx;
            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            bool usesNewParser = convertToSlnx || isOptInSlnParsingWithNewParser;
            var classLibrary1 = solution.ProjectsInOrder
                .FirstOrDefault(p => p.RelativePath == ConvertToUnixPathIfNeeded("ClassLibrary1\\ClassLibrary1.csproj", usesNewParser));
            Assert.NotNull(classLibrary1);
            Assert.Empty(classLibrary1.Dependencies);
            Assert.Null(classLibrary1.ParentProjectGuid);

            var myPhysicalFolderClassLibrary1 = solution.ProjectsInOrder
                .FirstOrDefault(p => p.RelativePath == ConvertToUnixPathIfNeeded("MyPhysicalFolder\\ClassLibrary1\\ClassLibrary1.csproj", usesNewParser));
            Assert.NotNull(myPhysicalFolderClassLibrary1);
            Assert.Empty(myPhysicalFolderClassLibrary1.Dependencies);

            var classLibrary2 = solution.ProjectsInOrder
                .FirstOrDefault(p => p.RelativePath == ConvertToUnixPathIfNeeded("ClassLibrary2\\ClassLibrary2.csproj", usesNewParser));
            Assert.NotNull(classLibrary2);
            Assert.Empty(classLibrary2.Dependencies);

            // When converting to slnx, the guids are not preserved.
            if (!convertToSlnx)
            {
                Assert.Equal("{E0F97730-25D2-418A-A7BD-02CAFDC6E470}", myPhysicalFolderClassLibrary1.ParentProjectGuid);
                Assert.Equal("{2AE8D6C4-FB43-430C-8AEB-15E5EEDAAE4B}", classLibrary2.ParentProjectGuid);
            }
            else
            {
                // try at least assert not null
                Assert.NotNull(myPhysicalFolderClassLibrary1.ParentProjectGuid);
                Assert.NotNull(classLibrary2.ParentProjectGuid);
            }
        }

        /// <summary>
        /// Verifies that hand-coded project-to-project dependencies listed in the .SLN file
        /// are correctly recognized by the solution parser.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void SolutionDependencies(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            Assert.Equal(3, solution.ProjectsInOrder.Count);

            var classLibrary1 = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary1");
            var classLibrary2 = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary2");
            var classLibrary3 = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary3");

            bool usesNewParser = convertToSlnx || isOptInSlnParsingWithNewParser;
            Assert.Equal(ConvertToUnixPathIfNeeded("ClassLibrary1\\ClassLibrary1.csproj", usesNewParser), classLibrary1.RelativePath);
            Assert.Single(classLibrary1.Dependencies);
            Assert.Equal(classLibrary3.ProjectGuid, classLibrary1.Dependencies[0]);
            Assert.Null(solution.ProjectsInOrder[0].ParentProjectGuid);

            Assert.Equal(ConvertToUnixPathIfNeeded("ClassLibrary2\\ClassLibrary2.csproj", usesNewParser), classLibrary2.RelativePath);
            Assert.Equal(2, classLibrary2.Dependencies.Count);
            // When converting to SLNX, the projects dependencies order is not preserved.
            Assert.Contains(classLibrary3.ProjectGuid, classLibrary2.Dependencies);
            Assert.Contains(classLibrary1.ProjectGuid, classLibrary2.Dependencies);
            Assert.Null(solution.ProjectsInOrder[1].ParentProjectGuid);

            Assert.Equal(ConvertToUnixPathIfNeeded("ClassLibrary3\\ClassLibrary3.csproj", usesNewParser), solution.ProjectsInOrder[2].RelativePath);
            Assert.Empty(solution.ProjectsInOrder[2].Dependencies);
            Assert.Null(solution.ProjectsInOrder[2].ParentProjectGuid);
        }

        /// <summary>
        /// Make sure the solution configurations get parsed correctly for a simple mixed C#/VC solution
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ParseSolutionConfigurations(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

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
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ParseSolutionConfigurationsNoMixedPlatform(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

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
        /// Make sure the project configurations in solution configurations get parsed correctly
        /// for a simple mixed C#/VC solution
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ParseProjectConfigurationsInSolutionConfigurations1(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
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
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            ProjectInSolution csharpProject = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary1");
            ProjectInSolution vcProject = solution.ProjectsInOrder.First(p => p.ProjectName == "MainApp");

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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ParseProjectConfigurationsInSolutionConfigurations2(bool isOptInSlnParsingWithNewParser, bool convertToSlnx)
        {
            string solutionFileContents =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                VisualStudioVersion = 17.11.35111.106
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "WinFormsApp1", "WinFormsApp1\WinFormsApp1.csproj", "{3B592A6A-6215-4675-9237-7FEB36BDB4F1}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{C25056E0-405C-4476-9B22-839264A8530C}"
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Win32 = Debug|Win32
                        Release|Win32 = Release|Win32
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {3B592A6A-6215-4675-9237-7FEB36BDB4F1}.Debug|Win32.ActiveCfg = Debug|x86
                        {3B592A6A-6215-4675-9237-7FEB36BDB4F1}.Debug|Win32.Build.0 = Debug|x86
                        {3B592A6A-6215-4675-9237-7FEB36BDB4F1}.Release|Win32.ActiveCfg = Release|x86
                        {3B592A6A-6215-4675-9237-7FEB36BDB4F1}.Release|Win32.Build.0 = Release|x86
                        {C25056E0-405C-4476-9B22-839264A8530C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {C25056E0-405C-4476-9B22-839264A8530C}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                    GlobalSection(ExtensibilityGlobals) = postSolution
                        SolutionGuid = {AA62B7C4-C703-4DBC-A7AD-D183666ECC20}
                    EndGlobalSection
                EndGlobal
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, isOptInSlnParsingWithNewParser, convertToSlnx);

            ProjectInSolution winFormsApp1 = solution.ProjectsInOrder.First(p => p.ProjectName == "WinFormsApp1");
            ProjectInSolution classLibrary1 = solution.ProjectsInOrder.First(p => p.ProjectName == "ClassLibrary1");

            Assert.Equal(2, winFormsApp1.ProjectConfigurations.Count);

            Assert.Equal("Debug|x86", winFormsApp1.ProjectConfigurations["Debug|Win32"].FullName);
            Assert.True(winFormsApp1.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assert.Equal("Release|x86", winFormsApp1.ProjectConfigurations["Release|Win32"].FullName);
            Assert.True(winFormsApp1.ProjectConfigurations["Debug|Win32"].IncludeInBuild);

            Assert.Equal(2, classLibrary1.ProjectConfigurations.Count);

            Assert.Equal("Debug|AnyCPU", classLibrary1.ProjectConfigurations["Debug|Any CPU"].FullName);
            Assert.False(classLibrary1.ProjectConfigurations["Debug|Any CPU"].IncludeInBuild);

            Assert.Equal("Release|AnyCPU", classLibrary1.ProjectConfigurations["Release|Any CPU"].FullName);
            Assert.False(classLibrary1.ProjectConfigurations["Release|Any CPU"].IncludeInBuild);
        }

        /// <summary>
        /// Helper method to create a SolutionFile object, and call it to parse the SLN file
        /// represented by the string contents passed in. Optionally can convert the SLN to SLNX and then parse the solution.
        /// </summary>
        private static SolutionFile ParseSolutionHelper(string solutionFileContents, bool isOptInSlnParsingWithNewParser, bool convertToSlnx = false)
        {
            solutionFileContents = solutionFileContents.Replace('\'', '"');
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                if (isOptInSlnParsingWithNewParser)
                {
                    testEnvironment.SetEnvironmentVariable("MSBUILD_PARSE_SLN_WITH_SOLUTIONPERSISTENCE", "1");
                }
                TransientTestFile sln = testEnvironment.CreateFile(FileUtilities.GetTemporaryFileName(".sln"), solutionFileContents);
                string solutionPath = convertToSlnx ? ConvertToSlnx(sln.Path) : sln.Path;
                return SolutionFile.Parse(solutionPath);
            }
        }

        private static string ConvertToSlnx(string slnPath)
        {
            string slnxPath = slnPath + "x";
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(slnPath).ShouldNotBeNull();
            SolutionModel solutionModel = serializer.OpenAsync(slnPath, CancellationToken.None).Result;
            SolutionSerializers.SlnXml.SaveAsync(slnxPath, solutionModel, CancellationToken.None).Wait();
            return slnxPath;
        }

        private static string ConvertToUnixPathIfNeeded(string path, bool usesNewParser)
        {
            // In the new parser, ProjectModel.FilePath is converted to Unix-style.
            return usesNewParser && !NativeMethodsShared.IsWindows ? path.Replace('\\', '/') : path;
        }
    }
}
