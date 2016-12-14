// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.Add.Proj.Tests
{
    public class GivenDotnetAddProj : TestBase
    {
        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"add project {helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Usage");
        }

        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters")]
        [InlineData("ihaveinv@lidcharacters")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"add {solutionName} project p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Could not find");
            cmd.StdOut.Should().Contain("Usage:");
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("InvalidSolution")
                                                    .WithLockFiles()
                                                    .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add InvalidSolution.sln project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Invalid solution ");
            cmd.StdOut.Should().Contain("Usage:");
        }

        [Fact]
        public void WhenInvalidSolutionIsFoundItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("InvalidSolution")
                                                    .WithLockFiles()
                                                    .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Invalid solution ");
            cmd.StdOut.Should().Contain("Usage:");
        }

        [Fact]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithSlnAndCsprojFiles")
                                                    .WithLockFiles()
                                                    .Path;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput(@"add App.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("You must specify at least one project to add.");
            cmd.StdOut.Should().Contain("Usage:");
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithSlnAndCsprojFiles")
                                                    .WithLockFiles()
                                                    .Path;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(Path.Combine(projectDirectory, "App"))
                .ExecuteWithCapturedOutput(@"add project App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("does not exist");
            cmd.StdOut.Should().Contain("Usage:");
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithMultipleSlnFiles")
                                                    .WithLockFiles()
                                                    .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("more than one");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles", "")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles", "{84A45D44-B677-492D-A6DA-B3A71135AB8E}")]
        public void WhenValidProjectIsPassedItGetsNormalizedAndAddedAndSlnBuilds(
            string testAsset,
            string projectGuid)
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance(testAsset)
                                                    .WithLockFiles()
                                                    .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var normalizedProjectPath = @"Lib\Lib.csproj";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the solution");
            cmd.StdErr.Should().BeEmpty();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var matchingProjects = slnFile.Projects
                .Where((p) => p.Name == "Lib")
                .ToList();

            matchingProjects.Count.Should().Be(1);
            var slnProject = matchingProjects[0];
            slnProject.FilePath.Should().Be(normalizedProjectPath);
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CPSProjectTypeGuid);
            if (!string.IsNullOrEmpty(projectGuid))
            {
                slnProject.Id.Should().Be(projectGuid);
            }

            var restoreCmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore {Path.Combine("App", "App.csproj")}");

            var buildCmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute("build App.sln");
            buildCmd.Should().Pass();
        }

        [Fact]
        public void WhenSolutionAlreadyContainsProjectItDoesntDuplicate()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithSlnAndExistingCsprojReferences")
                                                    .WithLockFiles()
                                                    .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already contains project");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenPassedMultipleProjectsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithSlnAndCsprojFiles")
                                                    .WithLockFiles()
                                                    .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd} idonotexist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("does not exist");
            cmd.StdErr.Should().NotMatchRegex("(.*does not exist.*){2,}");

            File.ReadAllText(slnFullPath)
                .Should().BeEquivalentTo(contentBefore);
        }
    }
}
