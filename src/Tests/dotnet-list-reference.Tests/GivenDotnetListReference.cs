// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLineValidation;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Msbuild.Tests.Utilities;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.List.Reference.Tests
{
    public class GivenDotnetListReference : SdkTest
    {
        private Func<string, string> ListProjectReferenceCommandHelpText = (defaultVal) => $@"Description:
  List all project-to-project references of the project.

Usage:
  dotnet [options] list [<PROJECT>] reference

Arguments:
  <PROJECT>    The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  -?, -h, --help    Show command line help.";

        private Func<string, string> ListCommandHelpText = (defaultVal) => $@"Description:
  List references or packages of a .NET project.

Usage:
  dotnet [options] list [<PROJECT | SOLUTION>] [command]

Arguments:
  <PROJECT | SOLUTION>    The project or solution file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  -?, -h, --help    Show command line help.

Commands:
  package      List all package references of the project or solution.
  reference    List all project-to-project references of the project.";

        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";

        public GivenDotnetListReference(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new ListReferenceCommand(Log).Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListProjectReferenceCommandHelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute("list", commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "list one two three reference".Split())
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup(identifier: projName);

            var cmd = new ListReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListProjectReferenceCommandHelpText(setup.TestRoot));
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            var cmd = new ListReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)                    
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.ProjectIsInvalid, "Broken/Broken.csproj"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListProjectReferenceCommandHelpText(setup.TestRoot));
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new ListReferenceCommand(Log)
                    .WithWorkingDirectory(workingDir)
                    .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, workingDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListProjectReferenceCommandHelpText(workingDir));
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new ListReferenceCommand(Log)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(ListProjectReferenceCommandHelpText(setup.TestRoot));
        }

        [Fact]
        public void WhenNoProjectReferencesArePresentInTheProjectItPrintsError()
        {
            var lib = NewLib(_testAssetsManager.CreateTestDirectory().Path);

            var cmd = new ListReferenceCommand(Log)
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

            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;

            var lib = NewLib(testDirectory, "lib");
            string ref1 = NewLib(testDirectory, "ref").CsProjPath;
            AddValidRef(ref1, lib);

            var cmd = new ListReferenceCommand(Log)
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

            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            var lib = NewLib(testDir, "lib");
            string ref1 = NewLib(testDir, "ref1").CsProjPath;
            string ref2 = NewLib(testDir, "ref2").CsProjPath;
            string ref3 = NewLib(testDir, "ref3").CsProjPath;

            AddValidRef(ref1, lib);
            AddValidRef(ref2, lib);
            AddValidRef(ref3, lib);

            var cmd = new ListReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                _testAssetsManager.CopyTestAsset(TestSetup.ProjectName, callingMethod: callingMethod, identifier: identifier, testAssetSubdirectory: TestSetup.TestGroup)
                    .WithSource()
                    .Path);
        }

        private ProjDir NewLib(string basePath, string testProjectName = "temp")
        {
            var dir = new ProjDir(Path.Combine(basePath, testProjectName));

            Directory.CreateDirectory(dir.Path);

            try
            {
                new DotnetCommand(Log, "new", "classlib", "-o", dir.Path, "--debug:ephemeral-hive", "--no-restore")
                    .WithWorkingDirectory(dir.Path)
                    .Execute()
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
            new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                .Execute(path)
                .Should().Pass();
        }
    }
}
