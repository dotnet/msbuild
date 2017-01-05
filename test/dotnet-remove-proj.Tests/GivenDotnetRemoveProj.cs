// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.Remove.Project.Tests
{
    public class GivenDotnetRemoveProj : TestBase
    {
        private const string HelpText = @".NET Remove Project from Solution Command

Usage: dotnet remove <PROJECT_OR_SOLUTION> project [options] [args]

Arguments:
  <PROJECT_OR_SOLUTION>  The project or solution to operation on. If a file is not specified, the current directory is searched.

Options:
  -h|--help  Show help information

Additional Arguments:
 Projects to remove from a solution
";

        private const string ExpectedSlnContentsAfterRemove = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnContentsAfterRemoveAllProjects = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Global
EndGlobal
";

        private const string ExpectedSlnContentsAfterRemoveNestedProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""src"", ""src"", ""{7B86CE74-F620-4B32-99FE-82D40F8D6BF2}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""Lib"", ""Lib"", ""{EAB71280-AF32-4531-8703-43CDBA261AA3}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""{84A45D44-B677-492D-A6DA-B3A71135AB8E}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.ActiveCfg = Debug|x64
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.Build.0 = Debug|x64
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.ActiveCfg = Debug|x86
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.Build.0 = Debug|x86
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.Build.0 = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.ActiveCfg = Release|x64
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.Build.0 = Release|x64
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.ActiveCfg = Release|x86
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{EAB71280-AF32-4531-8703-43CDBA261AA3} = {7B86CE74-F620-4B32-99FE-82D40F8D6BF2}
		{84A45D44-B677-492D-A6DA-B3A71135AB8E} = {EAB71280-AF32-4531-8703-43CDBA261AA3}
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnContentsAfterRemoveLastNestedProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
EndGlobal
";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"remove project {helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("remove one.sln two.sln three.sln project");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Unrecognized command or argument 'two.sln'");
            cmd.StdOut.Should().Be("Specify --help for a list of available options and commands.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"remove {commandName}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Required command was not provided.");
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

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove InvalidSolution.sln project {projectToRemove}");
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

            var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Invalid solution `{solutionPath}`. Invalid format in line 1: File header is missing");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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
            cmd.StdErr.Should().Be("You must specify at least one project to remove.");
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

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(solutionPath)
                .ExecuteWithCapturedOutput(@"remove project App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Specified solution file {solutionPath + Path.DirectorySeparatorChar} does not exist, or there is no solution file in the directory.");
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

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be($"Found more than one solution file in {projectDirectory + Path.DirectorySeparatorChar}. Please specify which one to use.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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
            cmd.StdOut.Should().Be("Project reference `referenceDoesNotExistInSln.csproj` could not be found.");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
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
            cmd.StdOut.Should().Be($"Project reference `{projectToRemove}` removed.");

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
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

            string outputText = $@"Project reference `{projectToRemove}` removed.
Project reference `{projectToRemove}` removed.";
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
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

            string outputText = $@"Project reference `idontexist.csproj` could not be found.
Project reference `{projectToRemove}` removed.
Project reference `idontexisteither.csproj` could not be found.";
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Fact]
        public void WhenReferenceIsRemovedBuildConfigsAreAlsoRemoved()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojToRemove")
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

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemove);
        }

        [Fact]
        public void WhenReferenceIsRemovedSlnBuilds()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojToRemove")
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

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore App.sln")
                .Should().Pass();

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute("build App.sln --configuration Release")
                .Should().Pass();

            var reasonString = "should be built in release mode, otherwise it means build configurations are missing from the sln file";

            var releaseDirectory = Directory.EnumerateDirectories(
                Path.Combine(projectDirectory, "App", "bin"),
                "Release",
                SearchOption.AllDirectories);
            releaseDirectory.Count().Should().Be(1, $"App {reasonString}");
            Directory.EnumerateFiles(releaseDirectory.Single(), "App.dll", SearchOption.AllDirectories)
                .Count().Should().Be(1, $"App {reasonString}");
        }

        [Fact]
        public void WhenFinalReferenceIsRemovedEmptySectionsAreRemoved()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojToRemove")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var appPath = Path.Combine("App", "App.csproj");
            var libPath = Path.Combine("Lib", "Lib.csproj");
            var projectsToRemove = $"{libPath} {appPath}";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectsToRemove}");
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveAllProjects);
        }

        [Fact]
        public void WhenNestedProjectIsRemovedItsSolutionFoldersAreRemoved()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndCsprojInSubDirToRemove")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");

            var projectToRemove = Path.Combine("src", "NotLastProjInSrc", "NotLastProjInSrc.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveNestedProj);
        }

        [Fact]
        public void WhenFinalNestedProjectIsRemovedSolutionFoldersAreRemoved()
        {
            var projectDirectory = TestAssets
                .Get("TestAppWithSlnAndLastCsprojInSubDirToRemove")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");

            var projectToRemove = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"remove project {projectToRemove}");
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveLastNestedProj);
        }
    }
}
