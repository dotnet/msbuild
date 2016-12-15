// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Cli.Remove.P2P.Tests
{
    public class GivenDotnetRemoveProj : TestBase
    {
        private const string HelpText = @".NET Remove Project from Solution Command

Usage: dotnet remove <PROJECT_OR_SOLUTION> project [options] [args]

Arguments:
  <PROJECT_OR_SOLUTION>  The project or solution to operation on. If a file is not specified, the current directory is searched.

Options:
  -h|--help  Show help information

Args:
  Projects to remove from a solution
";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"remove project {helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain(HelpText);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("remove one.sln two.sln three.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Unrecognized command or argument 'two.sln'");
            cmd.StdOut.Should().Contain("Specify --help for a list of available options and commands.") ;
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
                .ExecuteWithCapturedOutput($"remove {solutionName} project p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain($"Could not find solution or directory `{solutionName}`.");
            cmd.StdOut.Should().Contain(HelpText);
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

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove InvalidSolution.sln project {projectToRemove}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Invalid solution `InvalidSolution.sln`.");
            cmd.StdOut.Should().Contain(HelpText);
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
            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain($"Invalid solution `{solutionPath}`.");
            cmd.StdOut.Should().Contain(HelpText);
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
                .ExecuteWithCapturedOutput(@"remove App.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("You must specify at least one project to remove.");
            cmd.StdOut.Should().Contain(HelpText);
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
                .ExecuteWithCapturedOutput(@"remove project App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain($"Specified solution file {solutionPath + Path.DirectorySeparatorChar} does not exist, or there is no solution file in the directory.");
            cmd.StdOut.Should().Contain(HelpText);
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

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain($"Found more than one solution file in {projectDirectory + Path.DirectorySeparatorChar}. Please specify which one to use.");
            cmd.StdOut.Should().Contain(HelpText);
        }

        [Fact]
        public void WhenPassedAReferenceNotInSlnItPrintsStatus()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndExistingCsprojReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(solutionPath);
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("remove project referenceDoesNotExistInSln.csproj");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Project reference `referenceDoesNotExistInSln.csproj` could not be found.");
            File.ReadAllText(solutionPath)
                .Should().Be(contentBefore);
        }

        [Fact]
        public void WhenPassedAReferenceItRemovesTheReferenceButNotOtherReferences()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndExistingCsprojReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"Project reference `{projectToRemove}` removed.");

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(@"App\App.csproj");
        }

        [Fact]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndDuplicateProjectReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(3);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"Project reference `{projectToRemove}` removed.");

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(@"App\App.csproj");
        }

        [Fact]
        public void WhenPassedMultipleReferencesAndOneOfThemDoesNotExistItRemovesTheOneThatExists()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndExistingCsprojReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project idontexist.csproj {projectToRemove} idontexisteither.csproj");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"Project reference `{projectToRemove}` removed.");

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(@"App\App.csproj");
        }
    }
}
