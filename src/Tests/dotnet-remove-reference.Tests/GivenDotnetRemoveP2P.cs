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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Remove.Reference.Tests
{
    public class GivenDotnetRemoveReference : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove a project-to-project reference from the project.

Usage:
  dotnet remove <PROJECT> reference <PROJECT_PATH>... [options]

Arguments:
  <PROJECT>         The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the referenced projects to remove.

Options:
  -f, --framework <FRAMEWORK>    Remove the reference only when targeting a specific framework.
  -?, -h, --help                 Show command line help.";

        private Func<string, string> RemoveCommandHelpText = (defaultVal) => $@"Description:
      .NET Remove Command
    
    Usage:
      dotnet remove <PROJECT> [command] [options]
    
    Arguments:
      <PROJECT>    The project file to operate on. If a file is not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
    
    Options:
      -?, -h, --help    Show command line help.
    
    Commands:
      package <PACKAGE_NAME>      Remove a NuGet package reference from the project.
      reference <PROJECT_PATH>    Remove a project-to-project reference from the project.";

        readonly string[] FrameworkNet451Args = new[] { "-f", "net451" };
        const string ConditionFrameworkNet451 = "== 'net451'";
        readonly string[] CurrentFramework = new[] { "-f", ToolsetInfo.CurrentTargetFramework };
        const string ConditionCurrentFramework = $"== '{ToolsetInfo.CurrentTargetFramework}'";
        static readonly string[] DefaultFrameworks = new string[] { ToolsetInfo.CurrentTargetFramework, "net451" };

        public GivenDotnetRemoveReference(ITestOutputHelper log) : base(log)
        {
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                _testAssetsManager.CopyTestAsset(TestSetup.ProjectName, callingMethod: callingMethod + nameof(GivenDotnetRemoveReference), identifier: identifier + callingMethod, testAssetSubdirectory: TestAssetSubdirectories.NonRestoredTestProjects)
                    .WithSource()
                    .Path);
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(_testAssetsManager.CreateTestDirectory(testName: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib(string dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var projDir = dir == null ? NewDir(callingMethod: callingMethod, identifier: identifier) : new ProjDir(dir);

            try
            {
                string [] newArgs = new[] { "classlib", "-o", projDir.Path, "--no-restore" };
                new DotnetCommand(Log, "new","--debug:ephemeral-hive")
                    .WithWorkingDirectory(projDir.Path)
                    .Execute(newArgs)
                .Should().Pass();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new Exception($"Intermittent error in `dotnet new` occurred when running it in dir `{projDir.Path}`\nException:\n{e}");
            }

            return projDir;
        }

        private static void SetTargetFrameworks(ProjDir proj, string[] frameworks)
        {
            var csproj = proj.CsProj();
            csproj.AddProperty("TargetFrameworks", string.Join(";", frameworks));
            csproj.Save();
        }

        private ProjDir NewLibWithFrameworks(string dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var ret = NewLib(dir, callingMethod: callingMethod, identifier: identifier);
            SetTargetFrameworks(ret, DefaultFrameworks);
            return ret;
        }

        private ProjDir GetLibRef(TestSetup setup)
        {
            return new ProjDir(setup.LibDir);
        }

        private ProjDir AddLibRef(TestSetup setup, ProjDir proj, params string[] additionalArgs)
        {
            var ret = GetLibRef(setup);
            new AddReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(additionalArgs.Concat(new[] { ret.CsProjPath }))
                .Should().Pass();

            return ret;
        }

        private ProjDir AddValidRef(TestSetup setup, ProjDir proj, params string [] frameworkArgs)
        {
            var ret = new ProjDir(setup.ValidRefDir);
            new AddReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(frameworkArgs.Concat(new[] { ret.CsProjPath }))
                .Should().Pass();

            return ret;
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new RemoveReferenceCommand(Log).Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            List<string> args = new List<string>();
            args.Add("remove");
            if (commandName != null)
            {
                args.Add(commandName);
            }

            var cmd = new DotnetCommand(Log)
                .Execute(args);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "add", "one", "two", "three", "reference", "proj.csproj")
                    .Execute();
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsError(string projName)
        {
            var setup = Setup(identifier: projName.GetHashCode().ToString());

            var cmd = new RemoveReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsError()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            string brokenFolder = Path.Combine(setup.TestRoot, "Broken");
            Directory.CreateDirectory(brokenFolder);
            string brokenProjectPath = Path.Combine(brokenFolder, "Broken.csproj");
            File.WriteAllText(brokenProjectPath, $@"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFrameworks>net451;{ToolsetInfo.CurrentTargetFramework}</TargetFrameworks>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include=""**\*.cs""/>
        <EmbeddedResource Include=""**\*.resx""/>
    <!--intentonally broken-->");

            var cmd = new RemoveReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.ProjectIsInvalid, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsError()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new RemoveReferenceCommand(Log)
                    .WithWorkingDirectory(workingDir)
                    .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, workingDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsError()
        {
            var setup = Setup();

            var cmd = new RemoveReferenceCommand(Log)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void ItRemovesRefWithoutCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void ItRemovesRefWithCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib, FrameworkNet451Args);

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { libref.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(libref.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [Fact]
        public void WhenTwoDifferentRefsArePresentItDoesNotRemoveBoth()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenRefWithoutCondIsNotThereItPrintsMessage()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);

            string csprojContentBefore = lib.CsProjContent();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceCouldNotBeFound, libref.CsProjPath));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [Fact]
        public void WhenRefWithCondIsNotThereItPrintsMessage()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);

            string csprojContentBefore = lib.CsProjContent();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { libref.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceCouldNotBeFound, libref.CsProjPath));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [Fact]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingNoCondItDoesNotRemoveOther()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(librefNoCond.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingCondItDoesNotRemoveOther()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { librefCond.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(1);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [Fact]
        public void WhenRefWithDifferentCondIsPresentItDoesNotRemoveIt()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCondNet451 = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefCondNetCoreApp10 = AddLibRef(setup, lib, CurrentFramework);

            var csprojBefore = lib.CsProj();
            int condNet451Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            int condNetCoreApp10Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionCurrentFramework);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { librefCondNet451.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condNet451Before - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNet451.Name, ConditionFrameworkNet451).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionCurrentFramework).Should().Be(condNetCoreApp10Before);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNetCoreApp10.Name, ConditionCurrentFramework).Should().Be(1);
        }

        [Fact]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithDoubledRef"));
            var libref = GetLibRef(setup);

            string removedText = $@"{string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, setup.LibCsprojRelPath)}
{string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, setup.LibCsprojRelPath)}";

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
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
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
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
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
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
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenPassingMultipleReferencesItRemovesThemAll()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            string outputText = $@"{string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName))}
{string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine(setup.ValidRefCsprojRelPath))}";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath, validref.CsProjPath);
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
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);
            var validref = AddValidRef(setup, lib);

            string outputText = $@"{string.Format(CommonLocalizableStrings.ProjectReferenceCouldNotBeFound, setup.LibCsprojPath)}
{string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine(setup.ValidRefCsprojRelPath))}";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath, validref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(validref.Name).Should().Be(0);
        }

        [Fact]
        public void WhenDirectoryContainingProjectIsGivenReferenceIsRemoved()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);
            var libref = AddLibRef(setup, lib);

            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(libref.CsProjPath);

            result.Should().Pass();
            result.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            result.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "Empty";
            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, Path.Combine(setup.TestRoot, reference)));
        }

        [Fact]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "MoreThanOne";
            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, Path.Combine(setup.TestRoot, reference)));
        }
    }
}
