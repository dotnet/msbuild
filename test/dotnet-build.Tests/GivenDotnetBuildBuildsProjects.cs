// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class GivenDotnetBuildBuildsProjects : TestBase
    {
        [Fact]
        public void It_builds_projects_with_Unicode_in_path()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestAppWithUnicodéPath")
                .WithLockFiles();

            var testProjectDirectory = testInstance.TestRoot;

            var buildCommand = new BuildCommand("");
            buildCommand.WorkingDirectory = testProjectDirectory;

            buildCommand.ExecuteWithCapturedOutput()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_builds_projects_with_Unicode_in_path_project_path_passed()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestAppWithUnicodéPath")
                .WithLockFiles();

            var testProject = Path.Combine(testInstance.TestRoot, "project.json");

            new BuildCommand(testProject)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_builds_projects_with_ruleset_relative_path()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestRuleSet")
                .WithLockFiles();

            new BuildCommand(Path.Combine("TestLibraryWithRuleSet", "project.json"), skipLoadProject: true)
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining("CA1001")
                .And
                .HaveStdErrContaining("CA2213")
                .And
                .NotHaveStdErrContaining("CA1018"); // this violation is hidden in the ruleset
        }

        [Fact]
        public void It_builds_projects_with_a_local_project_json_path()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestAppSimple")
                .WithLockFiles();

            new BuildCommand("project.json")
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_builds_projects_with_xmlDoc_and_spaces_in_the_path()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestLibraryWithXmlDoc", identifier: "With Space")
                .WithLockFiles();

            testInstance.TestRoot.Should().Contain(" ");

            var output = new DirectoryInfo(Path.Combine(testInstance.TestRoot, "output"));

            new BuildCommand("", output: output.FullName, framework: DefaultLibraryFramework)
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();

            output.Should().HaveFiles(new[]
            {
                "TestLibraryWithXmlDoc.dll",
                "TestLibraryWithXmlDoc.xml"
            });
        }

        [WindowsOnlyFact]
        public void It_builds_projects_targeting_net46_and_Roslyn()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("AppWithNet46AndRoslyn")
                .WithLockFiles();

            var testProject = Path.Combine(testInstance.TestRoot, "project.json");

            new BuildCommand(testProject)
                .Execute()
                .Should()
                .Pass();
        }
    }
}
