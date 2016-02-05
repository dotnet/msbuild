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

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class ProjectToProjectDependenciesIncrementalTest : IncrementalTestBase
    {
        private readonly string[] _projects = new[] { "L0", "L11", "L12", "L21", "L22" };

        public ProjectToProjectDependenciesIncrementalTest() : base(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects", "TestProjectToProjectDependencies"),
            "L0",
            "L0 L11 L12 L22 L21 L12 L22 " + Environment.NewLine)
        {
        }

        [Theory,
        InlineData("L0", new[] { "L0" }),
        InlineData("L11", new[] { "L0", "L11" }),
        InlineData("L12", new[] { "L0", "L11", "L12" }),
        InlineData("L22", new[] { "L0", "L11", "L12", "L22" }),
        InlineData("L21", new[] { "L0", "L11", "L21" })
        ]
        public void TestIncrementalBuildOfDependencyGraph(string projectToTouch, string[] expectedRebuiltProjects)
        {

            // first clean build; all projects required compilation
            var result1 = BuildProject();
            AssertRebuilt(result1, _projects);

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
            var dependencies = new[] { "L11", "L12", "L21", "L22" };
            
            // first clean build; all projects required compilation
            var result1 = BuildProject();
            AssertRebuilt(result1, _projects);

            // modify the source code of a leaf dependency
            TouchSourcesOfProject("L22");

            // second build with no dependencies and no incremental; only the root rebuilds
            var result2 = BuildProject(noDependencies: true, noIncremental: true);
            result2.Should().StdOutMatchPattern("Compiling.*L0.*");

            AssertResultDoesNotContainStrings(result2, dependencies);

            // third build with no dependencies but incremental; nothing rebuilds
            var result3 = BuildProject(noDependencies: true);
            result3.Should().HaveSkippedProjectCompilation("L0");
            AssertResultDoesNotContainStrings(result3, dependencies);
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
                buildResult.Should().HaveCompiledProject(rebuiltProject);
            }

            foreach (var skippedProject in SetDifference(_projects, expectedRebuilt))
            {
                buildResult.Should().HaveSkippedProjectCompilation(skippedProject);
            }
        }

        protected override string GetProjectDirectory(string projectName)
        {
            return Path.Combine(TempProjectRoot.Path, "src", projectName);
        }
    }
}