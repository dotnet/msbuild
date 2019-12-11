// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using CommandLocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Sln.List.Tests
{
    public class GivenDotnetSlnList : SdkTest
    {
        private const string HelpText = @"Usage: dotnet sln <SLN_FILE> list [options]

Arguments:
  <SLN_FILE>   The solution file to operate on. If not specified, the command will search the current directory for one.

Options:
  -h, --help   Show command line help.";

        private const string SlnCommandHelpText = @"Usage: dotnet sln [options] <SLN_FILE> [command]

Arguments:
  <SLN_FILE>   The solution file to operate on. If not specified, the command will search the current directory for one.

Options:
  -h, --help   Show command line help.

Commands:
  add <PROJECT_PATH>      Add one or more projects to a solution file.
  list                    List all projects in a solution file.
  remove <PROJECT_PATH>   Remove one or more projects from a solution file.";

        public GivenDotnetSlnList(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", "list", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(SlnCommandHelpText);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log)
                .Execute("sln", "one.sln", "two.sln", "three.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
        }

        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters.sln")]
        [InlineData("ihaveinv@lidcharacters.sln")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", solutionName, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution")
                .WithSource()
                .Path;
            
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "InvalidSolution.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenInvalidSolutionIsFoundListPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution")
                .WithSource()
                .Path;

            var solutionFullPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFullPath, LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryListPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var solutionDir = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDir)
                .Execute("sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenNoProjectsArePresentInTheSolutionItPrintsANoProjectMessage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithEmptySln")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(CommonLocalizableStrings.NoProjectsFound);
        }

        [Fact]
        public void WhenProjectsPresentInTheSolutionItListsThem()
        {
            var expectedOutput = $@"{CommandLocalizableStrings.ProjectsHeader}
{new string('-', CommandLocalizableStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(expectedOutput);
        }

        [Fact]
        public void WhenProjectsPresentInTheReadonlySolutionItListsThem()
        {
            var expectedOutput = $@"{CommandLocalizableStrings.ProjectsHeader}
{new string('-', CommandLocalizableStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences")
                .WithSource()
                .Path;

            var slnFileName = Path.Combine(projectDirectory, "App.sln");
            var attributes = File.GetAttributes(slnFileName);
            File.SetAttributes(slnFileName, attributes | FileAttributes.ReadOnly);
            
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(expectedOutput);
        }
    }
}
