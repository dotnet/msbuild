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
    public class RootOutputPathTests : SdkTest
    {
        public RootOutputPathTests(ITestOutputHelper log) : base(log)
        {
        }

        (List<TestProject> testProjects, TestAsset testAsset) GetTestProjects(bool setRootOutputInProject, [CallerMemberName] string callingMethod = "")
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

            if (setRootOutputInProject)
            {
                foreach (var testProject in testProjects)
                {
                    testProject.AdditionalProperties["RootOutputPath"] = "..\\artifacts";
                }
            }

            var testAsset = _testAssetsManager.CreateTestProjects(testProjects, callingMethod: callingMethod, identifier: setRootOutputInProject.ToString());

            if (!setRootOutputInProject)
            {
                File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                   """
                    <Project>
                        <PropertyGroup>
                            <RootOutputPath>$(MSBuildThisFileDirectory)\artifacts</RootOutputPath>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            return (testProjects, testAsset);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUsesRootOutputPathForBuild(bool setRootOutputInProject)
        {
            var (testProjects, testAsset) = GetTestProjects(setRootOutputInProject);

            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, setRootOutputInProject);

            foreach (var testProject in testProjects)
            {
                new FileInfo(Path.Combine(testAsset.TestRoot, "artifacts", "build", testProject.Name, "debug", testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUsesRootOutputPathForPublish(bool setRootOutputInProject)
        {
            var (testProjects, testAsset) = GetTestProjects(setRootOutputInProject);

            new DotnetCommand(Log, "publish")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, setRootOutputInProject, "release");

            foreach (var testProject in testProjects)
            {
                new FileInfo(Path.Combine(testAsset.TestRoot, "artifacts", "build", testProject.Name, "release", testProject.Name + ".dll"))
                    .Should()
                    .Exist();

                new FileInfo(Path.Combine(testAsset.TestRoot, "artifacts", "publish", testProject.Name, "release", testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItUsesRootOutputPathForPack(bool setRootOutputInProject)
        {
            var (testProjects, testAsset) = GetTestProjects(setRootOutputInProject);

            new DotnetCommand(Log, "pack")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, setRootOutputInProject, "release");

            foreach (var testProject in testProjects)
            {
                new FileInfo(Path.Combine(testAsset.TestRoot, "artifacts", "build", testProject.Name, "release", testProject.Name + ".dll"))
                    .Should()
                    .Exist();

                new FileInfo(Path.Combine(testAsset.TestRoot, "artifacts", "package", "release", testProject.Name + ".1.0.0.nupkg"))
                    .Should()
                    .Exist();
            }
        }

        void ValidateIntermediatePaths(TestAsset testAsset, IEnumerable<TestProject> testProjects, bool setRootOutputInProject, string configuration = "debug")
        {
            foreach (var testProject in testProjects)
            {
                if (setRootOutputInProject)
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

                    new DirectoryInfo(Path.Combine(testAsset.TestRoot, "artifacts", "obj", testProject.Name, configuration))
                        .Should()
                        .Exist();
                };
            }
        }
    }
}
