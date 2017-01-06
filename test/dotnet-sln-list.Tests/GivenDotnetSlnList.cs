// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.Sln.List.Tests
{
    public class GivenDotnetSlnList : TestBase
    {
        private const string HelpText = @".NET List project(s) in a solution file Command

Usage: dotnet sln <SLN_FILE> list [options]

Arguments:
  <SLN_FILE>  Solution file to operate on. If not specified, the command will search the current directory for one.

Options:
  -h|--help  Show help information";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"sln list {helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"sln {commandName}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Required command was not provided.");
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("sln one.sln two.sln three.sln list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Unrecognized command or argument 'two.sln'");
        }

        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters.sln")]
        [InlineData("ihaveinv@lidcharacters.sln")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"sln {solutionName} list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Could not find solution or directory `{solutionName}`.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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
            
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("sln InvalidSolution.sln list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Invalid solution `InvalidSolution.sln`. Invalid format in line 1: File header is missing");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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

            var solutionFullPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("sln list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Invalid solution `{solutionFullPath}`. Invalid format in line 1: File header is missing");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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

            var solutionDir = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(solutionDir)
                .ExecuteWithCapturedOutput("sln list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Specified solution file {solutionDir + Path.DirectorySeparatorChar} does not exist, or there is no solution file in the directory.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("sln list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Found more than one solution file in {projectDirectory + Path.DirectorySeparatorChar}. Please specify which one to use.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenNoProjectReferencesArePresentInTheSolutionItPrintsANoProjectMessage()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithEmptySln")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("sln list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be("No projects found in the solution.");
        }

        [Fact]
        public void WhenProjectReferencesArePresentInTheSolutionItListsThem()
        {
            string OutputText = $@"Project reference(s)
--------------------
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndExistingCsprojReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput("sln list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
        }
    }
}
