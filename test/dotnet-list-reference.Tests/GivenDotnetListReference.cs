// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Msbuild.Tests.Utilities;
using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Cli.List.Reference.Tests
{
    public class GivenDotnetListReference : TestBase
    {
        private const string HelpText = @"Usage: dotnet list <PROJECT> reference [options]

Arguments:
  <PROJECT>   The project file to operate on. If a file is not specified, the command will search the current directory for one.

Options:
  -h, --help   Show help information.
";

        private const string ListCommandHelpText = @"Usage: dotnet list [options] <PROJECT> [command]

Arguments:
  <PROJECT>   The project file to operate on. If a file is not specified, the command will search the current directory for one.

Options:
  -h, --help   Show help information.

Commands:
  reference   .NET Core Project-to-Project dependency viewer
  tool        Lists installed tools in the current development environment.
";

        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new ListReferenceCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"list {commandName}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListCommandHelpText);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new ListReferenceCommand()
                    .WithProject("one two three")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup();

            var cmd = new ListReferenceCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            var cmd = new ListReferenceCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.ProjectIsInvalid, "Broken/Broken.csproj"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new ListReferenceCommand()
                    .WithWorkingDirectory(workingDir)
                    .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, workingDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new ListReferenceCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenNoProjectReferencesArePresentInTheProjectItPrintsError()
        {
            var lib = NewLib();

            var cmd = new ListReferenceCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.NoReferencesFound, CommonLocalizableStrings.P2P, lib.CsProjPath));
        }

        [Fact]
        public void ItPrintsSingleReference()
        {
            string OutputText = CommonLocalizableStrings.ProjectReferenceOneOrMore;
            OutputText += $@"
{new string('-', OutputText.Length)}
..\ref\ref.csproj";

            var lib = NewLib("lib");
            string ref1 = NewLib("ref").CsProjPath;
            AddValidRef(ref1, lib);

            var cmd = new ListReferenceCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
        }

        [Fact]
        public void ItPrintsMultipleReferences()
        {
            string OutputText = CommonLocalizableStrings.ProjectReferenceOneOrMore;
            OutputText += $@"
{new string('-', OutputText.Length)}
..\ref1\ref1.csproj
..\ref2\ref2.csproj
..\ref3\ref3.csproj";

            var lib = NewLib("lib");
            string ref1 = NewLib("ref1").CsProjPath;
            string ref2 = NewLib("ref2").CsProjPath;
            string ref3 = NewLib("ref3").CsProjPath;

            AddValidRef(ref1, lib);
            AddValidRef(ref2, lib);
            AddValidRef(ref3, lib);

            var cmd = new ListReferenceCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                TestAssets.Get(TestSetup.TestGroup, TestSetup.ProjectName)
                    .CreateInstance(callingMethod: callingMethod, identifier: identifier)
                    .WithSourceFiles()
                    .Root
                    .FullName);
        }

        private ProjDir NewDir(string testProjectName = "temp", [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssets.CreateTestDirectory(testProjectName: testProjectName, callingMethod: callingMethod, identifier: identifier).FullName);
        }

        private ProjDir NewLib(string testProjectName = "temp", [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var dir = NewDir(testProjectName: testProjectName, callingMethod: callingMethod, identifier: identifier);

            try
            {
                string newArgs = $"classlib -o \"{dir.Path}\" --debug:ephemeral-hive --no-restore";
                new NewCommandShim()
                    .WithWorkingDirectory(dir.Path)
                    .ExecuteWithCapturedOutput(newArgs)
                .Should().Pass();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new Exception($"Intermittent error in `dotnet new` occurred when running it in dir `{dir.Path}`\nException:\n{e}");
            }

            return dir;
        }

        private void AddValidRef(string path, ProjDir proj)
        {
            new AddReferenceCommand()
                .WithProject(proj.CsProjPath)
                .Execute($"\"{path}\"")
                .Should().Pass();
        }
    }
}
