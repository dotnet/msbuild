// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class ProjectToProjectDependenciesIncrementalTest : IncrementalTestBase
    {
        private readonly string[] _projects = new[] { "L0", "L11", "L12", "L21", "L22" };
        private readonly string _appProject = "L0";

        
        private string MainProjectExe
        {
            get
            {
                return MainProject + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");
            }
        }

        public ProjectToProjectDependenciesIncrementalTest()
        {
            MainProject = "L0";
            ExpectedOutput = "L0 L11 L12 L22 L21 L12 L22 " + Environment.NewLine;

        }

        [Theory]
        [InlineData("1", "L0", new[] { "L0" })]
        [InlineData("2", "L11", new[] { "L0", "L11" })]
        [InlineData("3", "L12", new[] { "L0", "L11", "L12" })]
        [InlineData("4", "L22", new[] { "L0", "L11", "L12", "L22" })]
        [InlineData("5", "L21", new[] { "L0", "L11", "L21" })]
        public void TestIncrementalBuildOfDependencyGraph(string testIdentifer, string projectToTouch, string[] expectedRebuiltProjects)
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectToProjectDependencies", identifier: testIdentifer)
                                                .WithLockFiles()
                                                .WithBuildArtifacts();

            TestProjectRoot = testInstance.TestRoot;

            // second build; nothing changed; no project required compilation
            var result2 = BuildProject();
            AssertRebuilt(result2, Array.Empty<string>());

            //modify the source code of a project
            TouchSourcesOfProject(projectToTouch);

            // third build; all projects on the paths from touched project to root project need to be rebuilt
            var result3 = BuildProject();
            AssertRebuilt(result3, expectedRebuiltProjects);
        }

        [Fact]
        public void TestNoDependencyFlag()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectToProjectDependencies")
                                                .WithLockFiles()
                                                .WithBuildArtifacts();

            TestProjectRoot = testInstance.TestRoot;

            var dependencies = new[] { "L11", "L12", "L21", "L22" };

            // modify the source code of a leaf dependency
            TouchSourcesOfProject("L22");

            // second build with no dependencies and no incremental; only the root rebuilds
            var result2 = BuildProject(noDependencies: true, noIncremental: true);
            result2.Should().HaveStdOutMatching("Compiling.*L0.*");

            AssertResultDoesNotContainStrings(result2, dependencies);

            // third build with no dependencies but incremental; nothing rebuilds
            var result3 = BuildProject(noDependencies: true);
            result3.Should().HaveSkippedProjectCompilation("L0", _appFrameworkFullName);
            AssertResultDoesNotContainStrings(result3, dependencies);
        }

        [Fact]
        public void TestNoDependenciesDependencyRebuild()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectToProjectDependencies")
                                                .WithLockFiles()
                                                .WithBuildArtifacts();

            TestProjectRoot = testInstance.TestRoot;

            // modify the source code of a leaf dependency
            TouchSourcesOfProject("L11");

            // second build with no dependencies, rebuilding leaf
            var result2 = new BuildCommand(GetProjectDirectory("L11"), noDependencies: true, framework: DefaultLibraryFramework).ExecuteWithCapturedOutput();
            result2.Should().HaveStdOutMatching("Compiling.*L11.*");

            // third build with no dependencies but incremental; root project should rebuild
            var result3 = BuildProject(noDependencies: true);
            result3.Should().HaveCompiledProject("L0", _appFrameworkFullName);
        }

        private static void AssertResultDoesNotContainStrings(CommandResult commandResult, string[] strings)
        {
            foreach (var s in strings)
            {
                commandResult.StdOut.Should().NotContain(s);
            }
        }

        // compute A - B
        private T[] SetDifference<T>(T[] A, T[] B)
        {
            var setA = new HashSet<T>(A);
            setA.ExceptWith(B);
            return setA.ToArray();
        }

        private void AssertRebuilt(CommandResult buildResult, string[] expectedRebuilt)
        {
            foreach (var rebuiltProject in expectedRebuilt)
            {
                string frameworkFullName = null;

                if (TestProjectIsApp(rebuiltProject))
                {
                    buildResult
                        .Should()
                        .HaveCompiledProject(rebuiltProject, frameworkFullName: _appFrameworkFullName);
                }
                else
                {
                    buildResult
                        .Should()
                        .HaveCompiledProject(rebuiltProject, _libraryFrameworkFullName);
                }
            }

            foreach (var skippedProject in SetDifference(_projects, expectedRebuilt))
            {
                if (TestProjectIsApp(skippedProject))
                {  
                    buildResult.Should().HaveSkippedProjectCompilation(skippedProject, _appFrameworkFullName);
                }
                else
                {
                    buildResult.Should().HaveSkippedProjectCompilation(skippedProject, _libraryFrameworkFullName);
                }
            }
        }

        private bool TestProjectIsApp(string testproject)
        {
            return testproject.Equals(_appProject, StringComparison.OrdinalIgnoreCase);
        }

        protected override string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TestProjectRoot, "src", projectName);
        }

        protected override string GetOutputDir()
        {
            return "";
        }

        protected override string GetOutputExePath()
        {
            var outputExe = Directory.GetFiles(TestProjectRoot, MainProjectExe, SearchOption.AllDirectories)
                                     .FirstOrDefault();

            if (string.IsNullOrEmpty(outputExe))
            {
                throw new FileNotFoundException($"Unable to find {outputExe} in {TestProjectRoot} or its subdirectories");
            }

            return Path.GetDirectoryName(outputExe);
        }
    }
}