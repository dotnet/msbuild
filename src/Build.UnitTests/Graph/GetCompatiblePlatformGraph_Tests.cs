// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    }
}
