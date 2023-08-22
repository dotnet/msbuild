// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using CommandLocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Sln.List.Tests
{
    public class GivenDotnetSlnList : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  List all projects in a solution file.

Usage:
  dotnet sln <SLN_FILE> list [options]

Arguments:
  <SLN_FILE>    The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  --solution-folders  Display solution folder paths.
  -?, -h, --help    Show command line help.";


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
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
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
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log)
                .Execute("sln", "one.sln", "two.sln", "three.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
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
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "InvalidSolution.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
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
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
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
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
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

        [Fact]
        public void WhenProjectsInSolutionFoldersPresentInTheSolutionItListsSolutionFolderPaths()
        {
            string[] expectedOutput = { $"{CommandLocalizableStrings.SolutionFolderHeader}",
$"{new string('-', CommandLocalizableStrings.SolutionFolderHeader.Length)}",
$"{Path.Combine("NestedSolution", "NestedFolder", "NestedFolder")}" };

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "list", "--solution-folders");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }
    }
}
