// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Msbuild.Tests.Utilities;
using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Cli.Remove.P2P.Tests
{
    public class GivenDotnetRemoveP2P : TestBase
    {
        private const string HelpText = @".NET Remove Project to Project (p2p) reference Command

Usage: dotnet remove <PROJECT_OR_SOLUTION> p2p [options] [args]

Arguments:
  <PROJECT_OR_SOLUTION>  The project or solution to operation on. If a file is not specified, the current directory is searched.

Options:
  -h|--help                   Show help information
  -f|--framework <FRAMEWORK>  Remove reference only when targetting a specific framework

Additional Arguments:
 Project to project references to remove
";

        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";
        static readonly string[] DefaultFrameworks = new string[] { "netcoreapp1.0", "net451" };

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

        private static void SetTargetFrameworks(ProjDir proj, string[] frameworks)
        {
            var csproj = proj.CsProj();
            csproj.AddProperty("TargetFrameworks", string.Join(";", frameworks));
            csproj.Save();
        }

        private ProjDir NewLibWithFrameworks([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var ret = NewLib(callingMethod: callingMethod, identifier: identifier);
            SetTargetFrameworks(ret, DefaultFrameworks);
            return ret;
        }

        private ProjDir GetLibRef(TestSetup setup)
        {
            return new ProjDir(setup.LibDir);
        }

        private ProjDir AddLibRef(TestSetup setup, ProjDir proj, string additionalArgs = "")
        {
            var ret = GetLibRef(setup);
            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(proj.CsProjPath)
                .Execute($"{additionalArgs} \"{ret.CsProjPath}\"")
                .Should().Pass();

            return ret;
        }

        private ProjDir AddValidRef(TestSetup setup, ProjDir proj, string frameworkArg = "")
        {
            var ret = new ProjDir(setup.ValidRefDir);
            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(proj.CsProjPath)
                .Execute($"{frameworkArg} \"{ret.CsProjPath}\"")
                .Should().Pass();

            return ret;
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new RemoveP2PCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
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

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new AddP2PCommand()
                    .WithProject("one two three")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be("Unrecognized command or argument 'two'");
            cmd.StdOut.Should().Be("Specify --help for a list of available options and commands.");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup();

            var cmd = new RemoveP2PCommand()
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

            var cmd = new RemoveP2PCommand()
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
            var cmd = new RemoveP2PCommand()
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

            var cmd = new RemoveP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be($"Could not find any project in `{setup.TestRoot + Path.DirectorySeparatorChar}`.");
            cmd.StdOut.Should().BeVisuallyEquivalentTo(HelpText);
        }

        [Fact]
        public void ItRemovesRefWithoutCondAndPrintsStatus()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = AddLibRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void ItRemovesRefWithCondAndPrintsStatus()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = AddLibRef(setup, lib, FrameworkNet451Arg);

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(libref.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [Fact]
        public void WhenTwoDifferentRefsArePresentItDoesNotRemoveBoth()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenRefWithoutCondIsNotThereItPrintsMessage()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = GetLibRef(setup);

            string csprojContetntBefore = lib.CsProjContent();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{libref.CsProjPath}` could not be found.");
            lib.CsProjContent().Should().BeEquivalentTo(csprojContetntBefore);
        }

        [Fact]
        public void WhenRefWithCondIsNotThereItPrintsMessage()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = GetLibRef(setup);

            string csprojContetntBefore = lib.CsProjContent();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{libref.CsProjPath}` could not be found.");
            lib.CsProjContent().Should().BeEquivalentTo(csprojContetntBefore);
        }

        [Fact]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingNoCondItDoesNotRemoveOther()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Arg);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{librefNoCond.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingCondItDoesNotRemoveOther()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Arg);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{librefCond.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(1);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [Fact]
        public void WhenRefWithDifferentCondIsPresentItDoesNotRemoveIt()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var librefCondNet451 = AddLibRef(setup, lib, FrameworkNet451Arg);
            var librefCondNetCoreApp10 = AddLibRef(setup, lib, FrameworkNetCoreApp10Arg);

            var csprojBefore = lib.CsProj();
            int condNet451Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            int condNetCoreApp10Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNetCoreApp10);
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{librefCondNet451.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condNet451Before - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNet451.Name, ConditionFrameworkNet451).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNetCoreApp10).Should().Be(condNetCoreApp10Before);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNetCoreApp10.Name, ConditionFrameworkNetCoreApp10).Should().Be(1);
        }

        [Fact]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithDoubledRef"));
            var libref = GetLibRef(setup);

            string removedText = $@"Project reference `{setup.LibCsprojRelPath}` removed.
Project reference `{setup.LibCsprojRelPath}` removed.";

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(proj.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(removedText);

            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingRefWithRelPathItRemovesRefWithAbsolutePath()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{setup.ValidRefCsprojRelToOtherProjPath}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingRefWithRelPathToProjectItRemovesRefWithPathRelToProject()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{setup.ValidRefCsprojRelToOtherProjPath}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingRefWithAbsolutePathItRemovesRefWithRelPath()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be($"Project reference `{setup.ValidRefCsprojRelToOtherProjPath}` removed.");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingMultipleReferencesItRemovesThemAll()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            string outputText = $@"Project reference `{Path.Combine(TestSetup.ProjectName, "Lib", setup.LibCsprojName)}` removed.
Project reference `{Path.Combine(TestSetup.ProjectName, setup.ValidRefCsprojRelPath)}` removed.";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\" \"{validref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
            csproj.NumberOfProjectReferencesWithIncludeContaining(validref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingMultipleReferencesAndOneOfThemDoesNotExistItRemovesOne()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();
            var libref = GetLibRef(setup);
            var validref = AddValidRef(setup, lib);

            string OutputText = $@"Project reference `{setup.LibCsprojPath}` could not be found.
Project reference `{Path.Combine(TestSetup.ProjectName, setup.ValidRefCsprojRelPath)}` removed.";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{libref.CsProjPath}\" \"{validref.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(validref.Name).Should().Be(0);
        }
    }
}
