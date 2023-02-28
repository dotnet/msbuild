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
        public void ItUsesRootOutputPathForPack(bool useDirectoryBuildProps)
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
