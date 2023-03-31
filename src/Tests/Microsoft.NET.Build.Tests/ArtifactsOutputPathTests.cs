// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Tests
{
    using ArtifactsTestExtensions;

    public class ArtifactsOutputPathTests : SdkTest
    {
        public ArtifactsOutputPathTests(ITestOutputHelper log) : base(log)
        {
        }

        (List<TestProject> testProjects, TestAsset testAsset) GetTestProjects(bool useDirectoryBuildProps, [CallerMemberName] string callingMethod = "")
        {
            var testProject1 = new TestProject()
            {
                Name = "App1",
                IsExe = true
            };

            var testProject2 = new TestProject()
            {
                Name = "App2",
                IsExe = true
            };

            var testLibraryProject = new TestProject()
            {
                Name = "Library",
            };

            testProject1.ReferencedProjects.Add(testLibraryProject);
            testProject2.ReferencedProjects.Add(testLibraryProject);

            List<TestProject> testProjects = new() { testProject1, testProject2, testLibraryProject };

            foreach (var testProject in testProjects)
            {
                testProject.UseArtifactsOutput = true;
                testProject.UseDirectoryBuildPropsForArtifactsOutput = useDirectoryBuildProps;
            }

            var testAsset = _testAssetsManager.CreateTestProjects(testProjects, callingMethod: callingMethod, identifier: useDirectoryBuildProps.ToString());

            if (useDirectoryBuildProps)
            {
                File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                   """
                    <Project>
                        <PropertyGroup>
                            <UseArtifactsOutput>true</UseArtifactsOutput>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            return (testProjects, testAsset);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUsesArtifactsOutputPathForBuild(bool useDirectoryBuildProps)
        {
            var (testProjects, testAsset) = GetTestProjects(useDirectoryBuildProps);

            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, useDirectoryBuildProps);

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUsesArtifactsOutputPathForPublish(bool useDirectoryBuildProps)
        {
            var (testProjects, testAsset) = GetTestProjects(useDirectoryBuildProps);

            new DotnetCommand(Log, "publish")
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, useDirectoryBuildProps, "release");

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
                new FileInfo(Path.Combine(outputPathCalculator.GetPublishDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUseArtifactstOutputPathForPack(bool useDirectoryBuildProps)
        {
            var (testProjects, testAsset) = GetTestProjects(useDirectoryBuildProps);

            new DotnetCommand(Log, "pack")
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, useDirectoryBuildProps, "release");

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
                new FileInfo(Path.Combine(outputPathCalculator.GetPackageDirectory(configuration: "release"), testProject.Name + ".1.0.0.nupkg"))
                    .Should()
                    .Exist();
            }
        }

        void ValidateIntermediatePaths(TestAsset testAsset, IEnumerable<TestProject> testProjects, bool useDirectoryBuildProps, string configuration = "debug")
        {
            foreach (var testProject in testProjects)
            {
                if (!useDirectoryBuildProps)
                {
                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, testProject.Name))
                        .Should()
                        .HaveDirectory("obj");

                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, testProject.Name, "bin"))
                        .Should()
                        .NotExist();

                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, "artifacts", "obj"))
                        .Should()
                        .NotExist();
                }
                else
                {
                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, testProject.Name))
                        .Should()
                        .NotHaveSubDirectories();

                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, ".artifacts", "obj", testProject.Name, configuration))
                        .Should()
                        .Exist();
                };
            }
        }

        [Fact]
        public void ProjectsCanSwitchOutputFormats()
        {
            var testProject = new TestProject()
            {
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            //  Build without artifacts format
            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(OutputPathCalculator.FromProject(testAsset.Path, testProject).GetOutputDirectory())
                .Should()
                .Exist();

            //  Now build as if UseArtifactsOutput was set in project file
            new BuildCommand(testAsset)
                .Execute("/p:UseArtifactsOutput=true", "/p:ImportDirectoryBuildProps=false")
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, ".artifacts", "bin", "debug"))
                .Should()
                .Exist();

            //  Now add a Directory.Build.props file setting UseArtifactsOutput to true
            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UseArtifactsOutput>true</UseArtifactsOutput>
                  </PropertyGroup>
                </Project>
                """);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(OutputPathCalculator.FromProject(testAsset.Path, testProject).GetOutputDirectory())
                .Should()
                .Exist();

            //  Now go back to not using artifacts output format
            File.Delete(Path.Combine(testAsset.Path, "Directory.Build.props"));

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void ProjectsCanCustomizeOutputPathBasedOnTargetFramework()
        {
            var testProject = new TestProject("CustomizeArtifactsPath")
            {
                IsExe = true,
                TargetFrameworks = "net7.0;net8.0;netstandard2.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UseArtifactsOutput>true</UseArtifactsOutput>
                    <AfterTargetFrameworkInferenceTargets>$(MSBuildThisFileDirectory)\Directory.AfterTargetFrameworkInference.targets</AfterTargetFrameworkInferenceTargets>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.AfterTargetFrameworkInference.targets"), """
                <Project>
                  <PropertyGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETCoreApp'">
                    <ArtifactsPivots Condition="'$(_TargetFrameworkVersionWithoutV)' == '8.0'">NET8_$(Configuration)</ArtifactsPivots>
                    <ArtifactsPivots Condition="'$(_TargetFrameworkVersionWithoutV)' == '7.0'">NET7_$(Configuration)</ArtifactsPivots>
                  </PropertyGroup>
                </Project>
                """);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", testProject.Name, "NET8_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", testProject.Name, "debug_net8.0")).Should().NotExist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", testProject.Name, "debug_net7.0")).Should().NotExist();

            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "obj", testProject.Name, "NET8_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "obj", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "obj", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "obj", testProject.Name, "debug_net8.0")).Should().NotExist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "obj", testProject.Name, "debug_net7.0")).Should().NotExist();

            foreach (var targetFramework in testProject.TargetFrameworks.Split(';'))
            {
                new DotnetPublishCommand(Log, "-f", targetFramework)
                    .WithWorkingDirectory(Path.Combine(testAsset.Path, testProject.Name))
                    .Execute()
                    .Should()
                    .Pass();
            }

            //  Note that publish defaults to release configuration for .NET 8 but not prior TargetFrameworks
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "publish", testProject.Name, "NET8_Release")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "publish", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "publish", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.Path, testProject.Name))
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, ".artifacts", "package", "release")).Should().Exist();
            new FileInfo(Path.Combine(testAsset.Path, ".artifacts", "package", "release", testProject.Name + ".1.0.0.nupkg")).Should().Exist();
        }

        TestAsset CreateCustomizedTestProject(bool useDirectoryBuildProps, string propertyName, string propertyValue, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject("App")
            {
                IsExe = true
            };

            testProject.UseArtifactsOutput = true;
            testProject.UseDirectoryBuildPropsForArtifactsOutput = useDirectoryBuildProps;

            if (!useDirectoryBuildProps)
            {
                testProject.AdditionalProperties[propertyName] = propertyValue;
            }

            var testAsset = _testAssetsManager.CreateTestProjects(new[] { testProject }, callingMethod: callingMethod, identifier: useDirectoryBuildProps.ToString());

            if (useDirectoryBuildProps)
            {
                File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                   $"""
                    <Project>
                        <PropertyGroup>
                            <UseArtifactsOutput>true</UseArtifactsOutput>
                            <{propertyName}>{propertyValue}</{propertyName}>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            return testAsset;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ArtifactsPathCanBeSet(bool useDirectoryBuildProps)
        {
            var artifactsFolder = _testAssetsManager.CreateTestDirectory(identifier: "ArtifactsPath").Path;

            var testAsset = CreateCustomizedTestProject(useDirectoryBuildProps, "ArtifactsPath", artifactsFolder);

            new DotnetBuildCommand(testAsset)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            //  If ArtifactsPath is set, even in the project file itself, we still include the project name in the path,
            //  as the path used is likely to be shared between multiple projects
            new FileInfo(Path.Combine(artifactsFolder, "bin", "App", "debug", "App.dll"))
                .Should()
                .Exist();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BinOutputNameCanBeSet(bool useDirectoryBuildProps)
        {
            var testAsset = CreateCustomizedTestProject(useDirectoryBuildProps, "ArtifactsBinOutputName", "binaries");

            new DotnetBuildCommand(testAsset)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            if (useDirectoryBuildProps)
            {
                new FileInfo(Path.Combine(testAsset.Path, ".artifacts", "binaries", "App", "debug", "App.dll"))
                    .Should()
                    .Exist();
            }
            else
            {
                new FileInfo(Path.Combine(testAsset.Path, "App", ".artifacts", "binaries", "debug", "App.dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PublishOutputNameCanBeSet(bool useDirectoryBuildProps)
        {
            var testAsset = CreateCustomizedTestProject(useDirectoryBuildProps, "ArtifactsPublishOutputName", "published_app");

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            if (useDirectoryBuildProps)
            {
                new FileInfo(Path.Combine(testAsset.Path, ".artifacts", "published_app", "App", "release", "App.dll"))
                    .Should()
                    .Exist();
            }
            else
            {
                new FileInfo(Path.Combine(testAsset.Path, "App", ".artifacts", "published_app", "release", "App.dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackageOutputNameCanBeSet(bool useDirectoryBuildProps)
        {
            var testAsset = CreateCustomizedTestProject(useDirectoryBuildProps, "ArtifactsPackageOutputName", "package_output");

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            if (useDirectoryBuildProps)
            {
                new FileInfo(Path.Combine(testAsset.Path, ".artifacts", "package_output", "release", "App.1.0.0.nupkg"))
                    .Should()
                    .Exist();
            }
            else
            {
                new FileInfo(Path.Combine(testAsset.Path, "App", ".artifacts", "package_output", "release", "App.1.0.0.nupkg"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProjectNameCanBeSet(bool useDirectoryBuildProps)
        {
            var testAsset = CreateCustomizedTestProject(useDirectoryBuildProps, "ArtifactsProjectName", "Apps\\MyApp");

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .SetEnvironmentVariables(useDirectoryBuildProps)
                .Execute()
                .Should()
                .Pass();

            if (useDirectoryBuildProps)
            {
                new FileInfo(Path.Combine(testAsset.Path, ".artifacts", "bin", "Apps", "MyApp", "debug", "App.dll"))
                    .Should()
                    .Exist();
            }
            else
            {
                //  Note that customized ArtifactsProjectName doesn't have an impact here when the artifacts folder is already inside the project folder
                new FileInfo(Path.Combine(testAsset.Path, "App", ".artifacts", "bin", "debug", "App.dll"))
                    .Should()
                    .Exist();
            }
        }
    }

    namespace ArtifactsTestExtensions
    {
        static class Extensions
        {
            public static TestCommand SetEnvironmentVariables(this TestCommand command, bool useDirectoryBuildProps)
            {
                //  There is an empty Directory.Build.props file in the test execution root, to stop other files further up in the repo from
                //  impacting the tests.  So if a project set UseArtifactsOutput to true, the logic would find that file and put the output
                //  in that folder.  To simulate the situation where there is no Directory.Build.props, we turn it off via an environment
                //  variable.
                if (!useDirectoryBuildProps)
                {
                    return command.WithEnvironmentVariable("ImportDirectoryBuildProps", "false");
                }
                else
                {
                    return command;
                }
            }
        }
    }
}
