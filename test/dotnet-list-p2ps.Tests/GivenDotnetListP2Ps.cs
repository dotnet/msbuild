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
        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";
        const string UsageText = "Usage: dotnet list p2ps";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new ListP2PsCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Usage");
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
            cmd.StdErr.Should().Contain("Could not find");
            cmd.StdOut.Should().Contain(UsageText);
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
            cmd.StdErr.Should().Contain(" is invalid.");
            cmd.StdOut.Should().Contain(UsageText);
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(Path.Combine(setup.TestRoot, "MoreThanOne"))
                    .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("more than one");
            cmd.StdOut.Should().Contain(UsageText);
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new ListP2PsCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("not find any");
            cmd.StdOut.Should().Contain(UsageText);
        }

        [Fact]
        public void WhenNoProjectReferencesArePresentInTheProjectItPrintsError()
        {
            var lib = NewLib();

            var cmd = new ListP2PsCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("There are no Project to Project references in project");
        }

        [Fact]
        public void ItPrintsSingleReference()
        {
            var lib = NewLib();
            string ref1 = "someref.csproj";
            AddFakeRef(ref1, lib);

            var cmd = new ListP2PsCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Project reference(s)");
            cmd.StdOut.Should().Contain(ref1);
        }

        [Fact]
        public void ItPrintsMultipleReferences()
        {
            var lib = NewLib();
            string ref1 = "someref.csproj";
            string ref2 = @"..\someref2.csproj";
            string ref3 = @"..\abc\abc.csproj";

            AddFakeRef(ref1, lib);
            AddFakeRef(ref2, lib);
            AddFakeRef(ref3, lib);

            var cmd = new ListP2PsCommand()
                .WithProject(lib.CsProjPath)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Project reference(s)");
            cmd.StdOut.Should().Contain(ref1);
            cmd.StdOut.Should().Contain(ref2);
            cmd.StdOut.Should().Contain(ref3);
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

        private void AddFakeRef(string path, ProjDir proj)
        {
            new AddP2PCommand()
                .WithProject(proj.CsProjPath)
                .Execute($"--force \"{path}\"")
                .Should().Pass();
        }
    }
}
