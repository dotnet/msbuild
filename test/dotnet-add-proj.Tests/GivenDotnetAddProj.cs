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
        private string HelpText = @".NET Add Project to Solution Command

Usage: dotnet add <PROJECT_OR_SOLUTION> project [options] [args]

Arguments:
  <PROJECT_OR_SOLUTION>  The project or solution to operation on. If a file is not specified, the current directory is searched.

Options:
  -h|--help  Show help information

Additional Arguments:
 Projects to add to solution
";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"add project {helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"add {commandName}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Required command was not provided.");
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("add one.sln two.sln three.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Unrecognized command or argument 'two.sln'");
            cmd.StdOut.Should().Be("Specify --help for a list of available options and commands.");
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
            cmd.StdErr.Should().Be($"Could not find solution or directory `{solutionName}`.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssets
                .Get("InvalidSolution")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add InvalidSolution.sln project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Invalid solution `InvalidSolution.sln`.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Fact]
        public void WhenInvalidSolutionIsFoundItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssets
                .Get("InvalidSolution")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Invalid solution `{solutionPath}`.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Fact]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojFiles")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput(@"add App.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("You must specify at least one project to add.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojFiles")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(solutionPath)
                .ExecuteWithCapturedOutput(@"add project App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Specified solution file {solutionPath + Path.DirectorySeparatorChar} does not exist, or there is no solution file in the directory.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithMultipleSlnFiles")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add project {projectToAdd}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Found more than one solution file in {projectDirectory + Path.DirectorySeparatorChar}. Please specify which one to use.");
            cmd.StdOut.Should().Be(HelpText);
        }

        [Theory]
        [InlineData("TestAppWithSlnAndCsprojFiles", "")]
        [InlineData("TestAppWithSlnAndCsprojProjectGuidFiles", "{84A45D44-B677-492D-A6DA-B3A71135AB8E}")]
        public void WhenValidProjectIsPassedItGetsNormalizedAndAddedAndSlnBuilds(
            string testAsset,
            string projectGuid)
        {
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var projectToAdd = "Lib/Lib.csproj";
            var normalizedProjectPath = @"Lib\Lib.csproj";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project `{Path.Combine("Lib", "Lib.csproj")}` added to the solution.");
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
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndExistingCsprojReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Solution {solutionPath} already contains project {projectToAdd}.");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenPassedMultipleProjectsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojFiles")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add App.sln project {projectToAdd} idonotexist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Project `idonotexist.csproj` does not exist.");

            File.ReadAllText(slnFullPath)
                .Should().BeEquivalentTo(contentBefore);
        }
    }
}
