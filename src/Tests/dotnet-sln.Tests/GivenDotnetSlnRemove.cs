// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Sln.Remove.Tests
{
    public class GivenDotnetSlnRemove : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove one or more projects from a solution file.

Usage:
  dotnet sln <SLN_FILE> remove [<PROJECT_PATH>...] [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the projects to remove from the solution.

Options:
  -?, -h, --help    Show command line help.";

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

        private const string ExpectedSlnContentsAfterRemoveProjectWithDependencies = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.27110.0
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""App"", ""App\App.csproj"", ""{BB02B949-F6BD-4872-95CB-96A05B1FE026}""
	ProjectSection(ProjectDependencies) = postProject
		{D53E177A-8ECF-43D5-A01E-98B884D53CA6} = {D53E177A-8ECF-43D5-A01E-98B884D53CA6}
	EndProjectSection
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""First"", ""First\First.csproj"", ""{D53E177A-8ECF-43D5-A01E-98B884D53CA6}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{BB02B949-F6BD-4872-95CB-96A05B1FE026}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{BB02B949-F6BD-4872-95CB-96A05B1FE026}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{BB02B949-F6BD-4872-95CB-96A05B1FE026}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{BB02B949-F6BD-4872-95CB-96A05B1FE026}.Release|Any CPU.Build.0 = Release|Any CPU
		{D53E177A-8ECF-43D5-A01E-98B884D53CA6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D53E177A-8ECF-43D5-A01E-98B884D53CA6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D53E177A-8ECF-43D5-A01E-98B884D53CA6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D53E177A-8ECF-43D5-A01E-98B884D53CA6}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {F6D9A973-1CFD-41C9-84F2-1471C0FE67DF}
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnContentsAfterRemoveProjectInSolutionWithNestedSolutionItems = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.28307.705
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""NewFolder1"", ""NewFolder1"", ""{F39E429A-F91C-44F9-9025-EA6E41C99637}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""NewFolder2"", ""NewFolder2"", ""{1B19AA22-7DB3-4A0F-8B89-2B80040B2BF0}""
	ProjectSection(SolutionItems) = preProject
		TextFile1.txt = TextFile1.txt
	EndProjectSection
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""NestedSolution"", ""NestedSolution"", ""{2128FC1C-7B60-4BC4-9010-D7A97E1805EA}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""NestedFolder"", ""NestedFolder"", ""{7BC6A99C-7321-47A2-8CA5-B394195BD2B8}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""NestedFolder"", ""NestedFolder"", ""{F6B0D958-42FF-4123-9668-A77A4ABFE5BA}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ConsoleApp2"", ""ConsoleApp2\ConsoleApp2.csproj"", ""{A264F7DB-E1EF-4F99-96BE-C08F29CA494F}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{A264F7DB-E1EF-4F99-96BE-C08F29CA494F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A264F7DB-E1EF-4F99-96BE-C08F29CA494F}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A264F7DB-E1EF-4F99-96BE-C08F29CA494F}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A264F7DB-E1EF-4F99-96BE-C08F29CA494F}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{1B19AA22-7DB3-4A0F-8B89-2B80040B2BF0} = {F39E429A-F91C-44F9-9025-EA6E41C99637}
		{7BC6A99C-7321-47A2-8CA5-B394195BD2B8} = {2128FC1C-7B60-4BC4-9010-D7A97E1805EA}
		{F6B0D958-42FF-4123-9668-A77A4ABFE5BA} = {7BC6A99C-7321-47A2-8CA5-B394195BD2B8}
		{A264F7DB-E1EF-4F99-96BE-C08F29CA494F} = {F6B0D958-42FF-4123-9668-A77A4ABFE5BA}
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {D36A0DD6-BD9C-4B57-9441-693B33DB7C2D}
	EndGlobalSection
EndGlobal
";

        public GivenDotnetSlnRemove(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", "remove", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log)
                .Execute("sln", "one.sln", "two.sln", "three.sln", "remove");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
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

        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters")]
        [InlineData("ihaveinv@lidcharacters")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"sln", solutionName, "remove", "p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "InvalidSolution.sln", "remove", projectToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenInvalidSolutionIsFoundRemovePrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionPath, LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: "GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"sln", "App.sln", "remove");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryRemovePrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(@"sln", "remove", "App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenPassedAReferenceNotInSlnItPrintsStatus()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(solutionPath);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "remove", "referenceDoesNotExistInSln.csproj");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "referenceDoesNotExistInSln.csproj"));
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenPassedAReferenceItRemovesTheReferenceButNotOtherReferences()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove));

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Fact]
        public void WhenSolutionItemsExistInFolderParentFoldersAreNotRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);

            var projectToRemove = Path.Combine("ConsoleApp1", "ConsoleApp1.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove));

            slnFile = SlnFile.Read(solutionPath);
            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveProjectInSolutionWithNestedSolutionItems);
        }

        [Fact]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndDuplicateProjectReferences")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(3);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            string outputText = string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove);
            outputText += Environment.NewLine + outputText;
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Fact]
        public void WhenPassedMultipleReferencesAndOneOfThemDoesNotExistItRemovesTheOneThatExists()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", "idontexist.csproj", projectToRemove, "idontexisteither.csproj");
            cmd.Should().Pass();

            string outputText = $@"{string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "idontexist.csproj")}
{string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove)}
{string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "idontexisteither.csproj")}";

            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(1);
            slnFile.Projects[0].FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Fact]
        public void WhenReferenceIsRemovedBuildConfigsAreAlsoRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemove);
        }

        [Fact]
        public void WhenDirectoryContainingProjectIsGivenProjectIsRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "remove", "Lib");
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemove);
        }

        [Fact]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;
            var directoryToRemove = "Empty";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", directoryToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToRemove)));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;
            var directoryToRemove = "Multiple";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", directoryToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToRemove)));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenReferenceIsRemovedSlnBuilds()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore", "App.sln")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("build", "App.sln", "--configuration", "Release", "/p:ProduceReferenceAssembly=false")
                .Should().Pass();

            var reasonString = "should be built in release mode, otherwise it means build configurations are missing from the sln file";

            var outputCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "App"));

            new DirectoryInfo(outputCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);

            var outputDirectory = new DirectoryInfo(outputCalculator.GetOutputDirectory(configuration: "Release"));
            outputDirectory.Should().Exist();
            outputDirectory.Should().HaveFile("App.dll");
        }

        [Fact]
        public void WhenProjectIsRemovedSolutionHasUTF8BOM()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "App.sln", "remove", projectToRemove);
            cmd.Should().Pass();

            var preamble = Encoding.UTF8.GetPreamble();
            preamble.Length.Should().Be(3);
            using (var stream = new FileStream(Path.Combine(projectDirectory, "App.sln"), FileMode.Open))
            {
                var bytes = new byte[preamble.Length];
                stream.Read(bytes, 0, bytes.Length);
                bytes.Should().BeEquivalentTo(preamble);
            }
        }

        [Fact]
        public void WhenFinalReferenceIsRemovedEmptySectionsAreRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            SlnFile slnFile = SlnFile.Read(solutionPath);
            slnFile.Projects.Count.Should().Be(2);

            var appPath = Path.Combine("App", "App.csproj");
            var libPath = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", libPath, appPath);
            cmd.Should().Pass();

            var solutionContents = File.ReadAllText(solutionPath);

            solutionContents.Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveAllProjects);
        }

        [Fact]
        public void WhenNestedProjectIsRemovedItsSolutionFoldersAreRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");

            var projectToRemove = Path.Combine("src", "NotLastProjInSrc", "NotLastProjInSrc.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveNestedProj);
        }

        [Fact]
        public void WhenFinalNestedProjectIsRemovedSolutionFoldersAreRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndLastCsprojInSubDirToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");

            var projectToRemove = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveLastNestedProj);
        }

        [Fact]
        public void WhenProjectIsRemovedThenDependenciesOnProjectAreAlsoRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnProjectDependencyToRemove")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");

            var projectToRemove = Path.Combine("Second", "Second.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"sln", "remove", projectToRemove);
            cmd.Should().Pass();

            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnContentsAfterRemoveProjectWithDependencies);
        }

        [Fact]
        public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles")
                .WithSource()
                .Path;

            var projectArg = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "remove", "App.sln", projectArg);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo(
                string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, "App.sln") + Environment.NewLine
                + CommonLocalizableStrings.DidYouMean + Environment.NewLine
                 + $"  dotnet sln App.sln remove {projectArg}"
            );
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
    }
}
