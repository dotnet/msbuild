// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Msbuild.Tests.Utilities;
using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Cli.List.P2P.Tests
{
    public class GivenDotnetListP2Ps : TestBase
    {
        private const string HelpText = @".NET Core Project-to-Project dependency viewer

Usage: dotnet list <PROJECT> p2ps [options]

Arguments:
  <PROJECT>  The project file to operate on. If a file is not specified, the command will search the current directory for one.

Options:
  -h|--help  Show help information";

        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new ListP2PsCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"list {commandName}");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be("Required command was not provided.");
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new AddReferenceCommand()
                    .WithProject("one two three")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be("Unrecognized command or argument 'two'");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup();

            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be($"Could not find project or directory `{projName}`.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be("Project `Broken/Broken.csproj` is invalid.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(workingDir)
                    .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be($"Found more than one project in `{workingDir + Path.DirectorySeparatorChar}`. Please specify which one to use.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be($"Could not find any project in `{setup.TestRoot + Path.DirectorySeparatorChar}`.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void WhenNoProjectReferencesArePresentInTheProjectItPrintsError()
        {
            var lib = NewLib();

            var cmd = new ListP2PsCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"There are no Project to Project references in project {lib.CsProjPath}. ;; Project to Project is the type of the item being requested (project, package, p2p) and {lib.CsProjPath} is the object operated on (a project file or a solution file). ");
        }

        [Fact]
        public void ItPrintsSingleReference()
        {
            const string OutputText = @"Project reference(s)
--------------------
..\ItPrintsSingleReferenceref\ItPrintsSingleReferenceref.csproj";

            var lib = NewLib("ItPrintsSingleReference", "lib");
            string ref1 = NewLib("ItPrintsSingleReference", "ref").CsProjPath;
            AddValidRef(ref1, lib);

            var cmd = new ListP2PsCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
        }

        [Fact]
        public void ItPrintsMultipleReferences()
        {
            const string OutputText = @"Project reference(s)
--------------------
..\ItPrintsSingleReferenceref1\ItPrintsSingleReferenceref1.csproj
..\ItPrintsSingleReferenceref2\ItPrintsSingleReferenceref2.csproj
..\ItPrintsSingleReferenceref3\ItPrintsSingleReferenceref3.csproj";

            var lib = NewLib("ItPrintsSingleReference", "lib");
            string ref1 = NewLib("ItPrintsSingleReference", "ref1").CsProjPath;
            string ref2 = NewLib("ItPrintsSingleReference", "ref2").CsProjPath;
            string ref3 = NewLib("ItPrintsSingleReference", "ref3").CsProjPath;

            AddValidRef(ref1, lib);
            AddValidRef(ref2, lib);
            AddValidRef(ref3, lib);

            var cmd = new ListP2PsCommand()
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

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssetsManager.CreateTestDirectory(callingMethod: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var dir = NewDir(callingMethod: callingMethod, identifier: identifier);

            try
            {
                new NewCommand()
                    .WithWorkingDirectory(dir.Path)
                    .ExecuteWithCapturedOutput("-t Lib")
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
