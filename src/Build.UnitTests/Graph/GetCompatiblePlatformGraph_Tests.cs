// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Graph.UnitTests.GraphTestingUtilities;
using static Microsoft.Build.Graph.UnitTests.ProjectGraphTests;

#nullable disable

namespace Microsoft.Build.Graph.UnitTests
{
    /// <summary>
    /// Performs SetPlatform negotiation for all project references when opted
    /// in via the EnableDynamicPlatformResolution property.
    ///
    /// The static graph mirrors the negotiation during build to determine plartform for each node.
    /// These tests mirror GetCompatiblePlatform_Tests.cs in order to make sure they both are in sync.
    /// </summary>
    public class ProjectGraphSetPlatformTests
    {

        [Fact]
        public void ValidateGlobalPropertyCopyByValueNotReference()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x64</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" />
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                            <Platforms>AnyCPU</Platforms>
                                                        </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 1).ProjectInstance.GlobalProperties.ContainsKey("Platform").ShouldBeFalse();
            }
        }

        [Fact]
        public void ValidateSetPlatformOverride()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x64</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                    <SetPlatform>platform=x86</SetPlatform>
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                            <Platforms>x64;AnyCPU</Platforms>
                                                        </PropertyGroup>
                                                        <ItemGroup>
                                                            <ProjectReference Include=""$(MSBuildThisFileDirectory)3.proj"" >
                                                            </ProjectReference>
                                                        </ItemGroup>
                                                    </Project>");
                var proj3 = env.CreateFile("3.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <Platforms>AnyCPU;x86</Platforms>
                                                        </PropertyGroup>
                                                    </Project>");


                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
                GetFirstNodeWithProjectNumber(graph, 3).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
            }
        }

        [Fact]
        public void ValidateNegotiationOverride()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x64</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                    <OverridePlatformNegotiationValue>x86</OverridePlatformNegotiationValue>
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                            <Platforms>x64;AnyCPU</Platforms>
                                                            <Platform>x86</Platform>
                                                        </PropertyGroup>
                                                        <ItemGroup>
                                                            <ProjectReference Include=""$(MSBuildThisFileDirectory)3.proj"" >
                                                            </ProjectReference>
                                                        </ItemGroup>
                                                    </Project>");
                var proj3 = env.CreateFile("3.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <Platforms>AnyCPU;x86</Platforms>
                                                        </PropertyGroup>
                                                    </Project>");


                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties.ContainsKey("Platform").ShouldBeFalse();
                GetFirstNodeWithProjectNumber(graph, 3).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
            }
        }

        [Fact]
        public void ResolvesMultipleReferencesToSameProject()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x64</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" />
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)3.proj"" />
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                            <Platforms>AnyCPU</Platforms>
                                                        </PropertyGroup>
                                                        <ItemGroup>
                                                            <ProjectReference Include=""$(MSBuildThisFileDirectory)3.proj"" />
                                                        </ItemGroup>
                                                    </Project>");

                var proj3 = env.CreateFile("3.proj", @"
                                                    <Project>
                                                        <PropertyGroup>
                                                            <Platforms>AnyCPU</Platforms>
                                                        </PropertyGroup>
                                                    </Project>");


                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("AnyCPU");
                GetFirstNodeWithProjectNumber(graph, 3).ProjectInstance.GlobalProperties["Platform"].ShouldBe("AnyCPU");
                graph.ProjectNodes.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void ResolvesViaPlatformLookupTable()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>win32</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <Platforms>x64;x86;AnyCPU</Platforms>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x64");
            }
        }

        [Fact]
        public void ResolvesViaProjectReferencesPlatformLookupTable()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>win32</Platform>
                                                                                                <PlatformLookupTable>win32=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <Platforms>x64;x86;AnyCPU</Platforms>
                                                        <PlatformLookupTable>win32=x86</PlatformLookupTable>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
            }
        }

        [Fact]
        public void ResolvesViaAnyCPUDefault()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x86</Platform>
                                                                                                <PlatformLookupTable>AnyCPU=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <Platforms>x64;AnyCPU</Platforms>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("AnyCPU");
            }
        }

        [Fact]
        public void ResolvesViaSamePlatform()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x86</Platform>
                                                                                                <PlatformLookupTable>x86=AnyCPU</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <PlatformLookupTable></PlatformLookupTable>
                                                        <Platforms>x86;x64;AnyCPU</Platforms>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties["Platform"].ShouldBe("x86");
            }
        }

        [Fact]
        public void FailsToResolve()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x86</Platform>
                                                                                                <PlatformLookupTable>AnyCPU=x64</PlatformLookupTable>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <Platforms>x64</Platforms>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                // Here we are checking if platform is defined. in this case it should not be since Platorm would be set to the value this project defaults as
                // in order to avoid dual build errors we remove platform in order to avoid the edge case where a project has global platform set and does not have global platform set
                // yet still default to the same platform.
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GlobalProperties.ContainsKey("Platform").ShouldBeFalse();
            }
        }

        [Fact]
        public void PlatformIsChosenAsDefault()
        {
            using (var env = TestEnvironment.Create())
            {

                TransientTestFile entryProject = CreateProjectFile(env, 1, extraContent: @"<PropertyGroup>
                                                                                                <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                                                                                                <Platform>x64</Platform>
                                                                                            </PropertyGroup>
                                                                                            <ItemGroup>
                                                                                                <ProjectReference Include=""$(MSBuildThisFileDirectory)2.proj"" >
                                                                                                </ProjectReference>
                                                                                            </ItemGroup>");
                var proj2 = env.CreateFile("2.proj", @"
                                                    <Project>
                                                    <PropertyGroup>
                                                        <platform>x64</platform>
                                                        <Platforms>x86;AnyCPU</Platforms>
                                                    </PropertyGroup>
                                                    </Project>");

                ProjectGraph graph = new ProjectGraph(entryProject.Path);
                GetFirstNodeWithProjectNumber(graph, 2).ProjectInstance.GetPropertyValue("Platform").ShouldBe(GetFirstNodeWithProjectNumber(graph, 1).ProjectInstance.GetPropertyValue("Platform"));
            }
        }

        // Validate configurations are defined in project reference protocol
        [Fact]
        public void SolutionWithoutAllConfigurations()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                var firstProjectName = "1";
                var secondProjectName = "2";
                var thirdProjectName = "3";
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
                TransientTestFolder project1Folder = testEnvironment.CreateFolder(Path.Combine(folder.Path, firstProjectName), createFolder: true);
                TransientTestFolder project1SubFolder = testEnvironment.CreateFolder(Path.Combine(project1Folder.Path, firstProjectName), createFolder: true);
                TransientTestFile project1 = testEnvironment.CreateFile(project1SubFolder, $"{firstProjectName}.csproj",
                    @"<Project>
                        <PropertyGroup>
                             <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                             <Platform>x64</Platform>
                         </PropertyGroup>
                         <ItemGroup>
                             <ProjectReference Include=""$(MSBuildThisFileDirectory)\..\..\2\2\2.proj"" />
                             <ProjectReference Include=""$(MSBuildThisFileDirectory)\..\..\3\3\3.proj"" />
                         </ItemGroup>
                  </Project>
                    ");

                TransientTestFolder project2Folder = testEnvironment.CreateFolder(Path.Combine(folder.Path, secondProjectName), createFolder: true);
                TransientTestFolder project2SubFolder = testEnvironment.CreateFolder(Path.Combine(project2Folder.Path, secondProjectName), createFolder: true);
                TransientTestFile project2 = testEnvironment.CreateFile(project2SubFolder, $"{secondProjectName}.proj",
                    @"<Project>
                        <PropertyGroup>
                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                            <Platforms>AnyCPU;x64</Platforms>
                        </PropertyGroup>
                    </Project>
                    ");

                TransientTestFolder project3Folder = testEnvironment.CreateFolder(Path.Combine(folder.Path, thirdProjectName), createFolder: true);
                TransientTestFolder project3SubFolder = testEnvironment.CreateFolder(Path.Combine(project3Folder.Path, thirdProjectName), createFolder: true);
                TransientTestFile project3 = testEnvironment.CreateFile(project3SubFolder, $"{thirdProjectName}.proj",
                    @"<Project>
                        <PropertyGroup>
                            <EnableDynamicPlatformResolution>true</EnableDynamicPlatformResolution>
                            <Platforms>AnyCPU;x64</Platforms>
                        </PropertyGroup>
                    </Project>
                    ");


                // Slashes here (and in the .slnf) are hardcoded as backslashes intentionally to support the common case.
                TransientTestFile solutionFile = testEnvironment.CreateFile(folder, "SimpleProject.sln",
                    @"
                    Microsoft Visual Studio Solution File, Format Version 12.00
                    # Visual Studio Version 16
                    VisualStudioVersion = 16.0.29326.124
                    MinimumVisualStudioVersion = 10.0.40219.1
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Project1"", ""1\1\1.csproj"", ""{79B5EBA6-5D27-4976-BC31-14422245A59A}""
                    EndProject
                    Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""2"", ""2\2\2.proj"", ""{8EFCCA22-9D51-4268-90F7-A595E11FCB2D}""
                    EndProject
                    Global
                        GlobalSection(SolutionConfigurationPlatforms) = preSolution
                            Debug|x64 = Debug|x64
                            Release|x64 = Release|x64
                            EndGlobalSection
                        GlobalSection(ProjectConfigurationPlatforms) = postSolution
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Debug|x64.ActiveCfg = Debug|x64
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Debug|x64.Build.0 = Debug|x64
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Release|x64.ActiveCfg = Release|x64
                            {79B5EBA6-5D27-4976-BC31-14422245A59A}.Release|x64.Build.0 = Release|x64

                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Debug|x64.ActiveCfg = Debug|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Debug|x64.Build.0 = Debug|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Release|x64.ActiveCfg = Release|Any CPU
                            {8EFCCA22-9D51-4268-90F7-A595E11FCB2D}.Release|x64.Build.0 = Release|Any CPU
                        EndGlobalSection
                        GlobalSection(SolutionProperties) = preSolution
                            HideSolutionNode = FALSE
                        EndGlobalSection
                        GlobalSection(ExtensibilityGlobals) = postSolution
                            SolutionGuid = {DE7234EC-0C4D-4070-B66A-DCF1B4F0CFEF}
                        EndGlobalSection
                    EndGlobal
                ");

                ProjectCollection projectCollection = testEnvironment.CreateProjectCollection().Collection;
                MockLogger logger = new();
                projectCollection.RegisterLogger(logger);
                ProjectGraphEntryPoint entryPoint = new(solutionFile.Path, new Dictionary<string, string>());

                // We want to make sure negotiation respects configuration if defined but negotiates if not.
                ProjectGraph graphFromSolution = new(entryPoint, projectCollection);
                logger.AssertNoErrors();
                GetFirstNodeWithProjectNumber(graphFromSolution, 2).ProjectInstance.GetPropertyValue("Platform").ShouldBe("AnyCPU", "Project2 should have followed the sln config to AnyCPU");
                GetFirstNodeWithProjectNumber(graphFromSolution, 3).ProjectInstance.GetPropertyValue("Platform").ShouldBe("x64", "Project3 isn't in the solution so it should have negotiated to x64 to match Project1");
            }
        }
    }
}
