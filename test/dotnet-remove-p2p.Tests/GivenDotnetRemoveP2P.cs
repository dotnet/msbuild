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
        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";

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

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new RemoveP2PCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Usage");
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
            cmd.StdErr.Should().Contain("Could not find");
            cmd.StdOut.Should().Contain("Usage");
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
            cmd.StdErr.Should().Contain("Invalid project");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new RemoveP2PCommand()
                    .WithWorkingDirectory(Path.Combine(setup.TestRoot, "MoreThanOne"))
                    .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("more than one");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new RemoveP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("not find any");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void ItRemovesRefWithoutCondAndPrintsStatus()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ItRemovesRefWithConAndPrintsStatus()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenTwoRefsArePresentItDoesNotRemoveBoth()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenRefWithoutCondIsNotThereItPrintsMessage()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenRefWithCondIsNotThereItPrintsMessage()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenRefWithCondIsPresentAndRemovingNoCondItDoesNotRemoveIt()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenRefWithoutCondIsPresentAndRemovingRefWithCondItDoesNotRemoveIt()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void WhenRefWithDifferentCondIsPresentItDoesNotRemoveIt()
        {
            throw new NotImplementedException();
        }
    }
}
