// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.UnitTests;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.VisualStudio.SolutionPersistence;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests.Construction
{
    public class SolutionFilter_Tests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private static readonly BuildEventContext _buildEventContext = new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0);

        public SolutionFilter_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Test that a solution filter file excludes projects not covered by its list of projects or their dependencies.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SolutionFilterFiltersProjects(bool graphBuild)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder classLibFolder = testEnvironment.CreateFolder(Path.Combine(folder.Path, "ClassLibrary"), createFolder: true);
                TransientTestFolder classLibSubFolder = testEnvironment.CreateFolder(Path.Combine(classLibFolder.Path, "ClassLibrary"), createFolder: true);
                TransientTestFile classLibrary = testEnvironment.CreateFile(classLibSubFolder, "ClassLibrary.csproj",
                    @"<Project>
                  <Target Name=""ClassLibraryTarget"">
                      <Message Text=""ClassLibraryBuilt""/>
                  </Target>
                  </Project>
                    ");

                TransientTestFolder simpleProjectFolder = testEnvironment.CreateFolder(Path.Combine(folder.Path, "SimpleProject"), createFolder: true);
                TransientTestFolder simpleProjectSubFolder = testEnvironment.CreateFolder(Path.Combine(simpleProjectFolder.Path, "SimpleProject"), createFolder: true);
                TransientTestFile simpleProject = testEnvironment.CreateFile(simpleProjectSubFolder, "SimpleProject.csproj",
                    @"<Project DefaultTargets=""SimpleProjectTarget"">
                  <Target Name=""SimpleProjectTarget"">
                      <Message Text=""SimpleProjectBuilt""/>
                  </Target>
                  </Project>
                    ");
                // Slashes here (and in the .slnf) are hardcoded as backslashes intentionally to support the common case.
                TransientTestFile solutionFile = testEnvironment.CreateFile(simpleProjectFolder, "SimpleProject.sln",
                    """
                    Microsoft Visual Studio Solution File, Format Version 12.00
                    # Visual Studio Version 16
                    VisualStudioVersion = 16.0.29326.124
                    MinimumVisualStudioVersion = 10.0.40219.1
                    Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "SimpleProject", "SimpleProject\SimpleProject.csproj", "{79B5EBA6-5D27-4976-BC31-14422245A59A}"
                    EndProject
                    Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "ClassLibrary", "..\ClassLibrary\ClassLibrary\ClassLibrary.csproj", "{8EFCCA22-9D51-4268-90F7-A595E11FCB2D}"
                    EndProject
                    Global
                        GlobalSection(SolutionConfigurationPlatforms) = preSolution
                            Debug|Any CPU = Debug|Any CPU
                            Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                        GlobalSection(ProjectConfigurationPlatforms) = postSolution
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Release|Any CPU.ActiveCfg = Release|Any CPU
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Release|Any CPU.Build.0 = Release|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Release|Any CPU.ActiveCfg = Release|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Release|Any CPU.Build.0 = Release|Any CPU
                            {06A4DD1B-5027-41EF-B72F-F586A5A83EA5}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                            {06A4DD1B-5027-41EF-B72F-F586A5A83EA5}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            {06A4DD1B-5027-41EF-B72F-F586A5A83EA5}.Release|Any CPU.ActiveCfg = Release|Any CPU
                            {06A4DD1B-5027-41EF-B72F-F586A5A83EA5}.Release|Any CPU.Build.0 = Release|Any CPU
                        EndGlobalSection
                        GlobalSection(SolutionProperties) = preSolution
                            HideSolutionNode = FALSE
                        EndGlobalSection
                        GlobalSection(ExtensibilityGlobals) = postSolution
                            SolutionGuid = {DE7234EC-0C4D-4070-B66A-DCF1B4F0CFEF}
                        EndGlobalSection
                    EndGlobal
                    """);
                TransientTestFile filterFile = testEnvironment.CreateFile(folder, "solutionFilter.slnf",
                    /*lang=json*/
                                  """
                                  {
                                    "solution": {
                                      // I'm a comment
                                      "path": ".\\SimpleProject\\SimpleProject.sln",
                                      "projects": [
                                      /* "..\\ClassLibrary\\ClassLibrary\\ClassLibrary.csproj", */
                                        "SimpleProject\\SimpleProject.csproj",
                                      ]
                                      }
                                  }
                                  """);
                Directory.GetCurrentDirectory().ShouldNotBe(Path.GetDirectoryName(filterFile.Path));
                if (graphBuild)
                {
                    ProjectCollection projectCollection = testEnvironment.CreateProjectCollection().Collection;
                    MockLogger logger = new();
                    projectCollection.RegisterLogger(logger);
                    ProjectGraphEntryPoint entryPoint = new(filterFile.Path, new Dictionary<string, string>());

                    // We only need to construct the graph, since that tells us what would build if we were to build it.
                    ProjectGraph graphFromSolution = new(entryPoint, projectCollection);
                    logger.AssertNoErrors();
                    graphFromSolution.ProjectNodes.ShouldHaveSingleItem();
                    graphFromSolution.ProjectNodes.Single().ProjectInstance.ProjectFileLocation.LocationString.ShouldBe(simpleProject.Path);
                }
                else
                {
                    SolutionFile solution = SolutionFile.Parse(filterFile.Path);
                    ILoggingService mockLogger = CreateMockLoggingService();
                    ProjectInstance[] instances = SolutionProjectGenerator.Generate(solution, null, null, _buildEventContext, mockLogger);
                    instances.ShouldHaveSingleItem();

                    // Check that dependencies are built, and non-dependencies in the .sln are not.
                    MockLogger logger = new(output);
                    instances[0].Build(targets: null, new List<ILogger> { logger }).ShouldBeTrue();
                    logger.AssertLogContains(new string[] { "SimpleProjectBuilt" });
                    logger.AssertLogDoesntContain("ClassLibraryBuilt");
                }
            }
        }

        [Theory]
        [InlineData(/*lang=json,strict*/ """
            {
              "solution": {
                "path": "C:\\notAPath\\MSBuild.Dev.sln",
                "projects2": [
                  "src\\Build\\Microsoft.Build.csproj",
                  "src\\Framework\\Microsoft.Build.Framework.csproj",
                  "src\\MSBuild\\MSBuild.csproj",
                  "src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj"
                ]
                }
            }
            """, "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(/*lang=json,strict*/ """
            [{
              "solution": {
                "path": "C:\\notAPath\\MSBuild.Dev.sln",
                "projects": [
                  "src\\Build\\Microsoft.Build.csproj",
                  "src\\Framework\\Microsoft.Build.Framework.csproj",
                  "src\\MSBuild\\MSBuild.csproj",
                  "src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj"
                ]
                }
            }]
            """, "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(/*lang=json,strict*/ """
            {
              "solution": {
                "path": "C:\\notAPath\\MSBuild.Dev.sln",
                "projects": [
                  {"path": "src\\Build\\Microsoft.Build.csproj"},
                  {"path": "src\\Framework\\Microsoft.Build.Framework.csproj"},
                  {"path": "src\\MSBuild\\MSBuild.csproj"},
                  {"path": "src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj"}
                ]
                }
            }
            """, "MSBuild.SolutionFilterJsonParsingError")]
        [InlineData(/*lang=json,strict*/ """
            {
              "solution": {
                "path": "C:\\notAPath2\\MSBuild.Dev.sln",
                "projects": [
                  {"path": "src\\Build\\Microsoft.Build.csproj"},
                  {"path": "src\\Framework\\Microsoft.Build.Framework.csproj"},
                  {"path": "src\\MSBuild\\MSBuild.csproj"},
                  {"path": "src\\Tasks.UnitTests\\Microsoft.Build.Tasks.UnitTests.csproj"}
                ]
                }
            }
            """, "MSBuild.SolutionFilterMissingSolutionError")]
        public void InvalidSolutionFilters([StringSyntax(StringSyntaxAttribute.Json)] string slnfValue, string exceptionReason)
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
        /// Test that a solution filter file is parsed correctly, and it can accurately respond as to whether a project should be filtered out.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ParseSolutionFilter(bool convertToSlnx)
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
                        ""path"": """ + (convertToSlnx ? ConvertToSlnx(sln.Path) : sln.Path).Replace("\\", "\\\\") + @""",
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

        [Fact]
        public void SolutionFilterWithSpecialSymbolInThePath()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            // Create folder with special symbols in the name
            folder = testEnvironment.CreateFolder(Path.Combine(folder.Path, $"test@folder%special$symbols"), createFolder: true);
            // Create simple solution and simple solution filter
            TransientTestFile sln = testEnvironment.CreateFile(folder, "SimpleSolution.sln",
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SolutionTest", "SolutionTest.csproj", "{767AA460-C33F-41C3-A8B6-4DA283263A51}"
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|Any CPU = Debug|Any CPU
                    Release|Any CPU = Release|Any CPU
                EndGlobalSection
                GlobalSection(SolutionProperties) = preSolution
                    HideSolutionNode = FALSE
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {767AA460-C33F-41C3-A8B6-4DA283263A51}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {767AA460-C33F-41C3-A8B6-4DA283263A51}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {767AA460-C33F-41C3-A8B6-4DA283263A51}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {767AA460-C33F-41C3-A8B6-4DA283263A51}.Release|Any CPU.Build.0 = Release|Any CPU
                EndGlobalSection
            EndGlobal
            """);
            TransientTestFile slnf = testEnvironment.CreateFile(folder, "SimpleSolution.slnf",
            """
            {
                "solution": {
                    "path": "SimpleSolution.sln",
                    "projects": [
                        "SolutionTest.csproj"
                    ]
                }
            }
            """);

            SolutionFile sp = SolutionFile.Parse(slnf.Path);

            // just assert that no error is thrown
            Assert.True(sp.ProjectShouldBuild("SolutionTest.csproj"));
        }

        private static string ConvertToSlnx(string slnPath)
        {
            string slnxPath = slnPath + "x";
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(slnPath).ShouldNotBeNull();
            SolutionModel solutionModel = serializer.OpenAsync(slnPath, CancellationToken.None).Result;
            SolutionSerializers.SlnXml.SaveAsync(slnxPath, solutionModel, CancellationToken.None).Wait();
            return slnxPath;
        }

        private ILoggingService CreateMockLoggingService()
        {
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 0);
            MockLogger logger = new(output);
            loggingService.RegisterLogger(logger);
            return loggingService;
        }
    }
}
