// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using HelpActual = Microsoft.DotNet.Tools.Help;

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetCommand : TestBase
    {
        private const string HelpText =
@"Usage: dotnet [runtime-options] [path-to-application]
Usage: dotnet [sdk-options] [command] [arguments] [command-options]

path-to-application:
  The path to an application .dll file to execute.

SDK commands:
  new              Initialize .NET projects.
  restore          Restore dependencies specified in the .NET project.
  run              Compiles and immediately executes a .NET project.
  build            Builds a .NET project.
  publish          Publishes a .NET project for deployment (including the runtime).
  test             Runs unit tests using the test runner specified in the project.
  pack             Creates a NuGet package.
  migrate          Migrates a project.json based project to a msbuild based project.
  clean            Clean build output(s).
  sln              Modify solution (SLN) files.
  add              Add reference to the project.
  remove           Remove reference from the project.
  list             List project references or installed tools.
  nuget            Provides additional NuGet commands.
  msbuild          Runs Microsoft Build Engine (MSBuild).
  vstest           Runs Microsoft Test Execution Command Line Tool.
  store            Stores the specified assemblies in the runtime store.
  tool             Modify tools.
  help             Show help.

Common options:
  -v|--verbosity        Set the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic].
  -h|--help             Show help.

Run 'dotnet COMMAND --help' for more information on a command.

sdk-options:
  --version        Display .NET Core SDK version.
  --info           Display .NET Core information.
  --list-sdks      Display the installed SDKs.
  --list-runtimes  Display the installed runtimes.
  -d|--diagnostics Enable diagnostic output.

runtime-options:
  --additionalprobingpath <path>    Path containing probing policy and assemblies to probe for.
  --fx-version <version>            Version of the installed Shared Framework to use to run the application.
  --roll-forward-on-no-candidate-fx Roll forward on no candidate shared framework is enabled.
  --additional-deps <path>          Path to additional deps.json file.";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedToDotnetItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"{helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenHelpCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new HelpCommand()
                .ExecuteWithCapturedOutput();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenInvalidCommandIsPassedToDotnetHelpItPrintsError()
        {
          var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("help invalid");

          cmd.Should().Fail();
          cmd.StdErr.Should().Contain(string.Format(Tools.Help.LocalizableStrings.CommandDoesNotExist, "invalid"));
          cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData("complete")]
        [InlineData("parse")]
        public void WhenCommandWithoutDocLinkIsPassedToDotnetHelpItPrintsError(string command)
        {
          var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput($"help {command}");

          cmd.Should().Fail();
          cmd.StdErr.Should().Contain(string.Format(Tools.Help.LocalizableStrings.CommandDoesNotExist, command));
          cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [WindowsOnlyFact]
        public void WhenRunOnWindowsDotnetHelpCommandShouldContainProperProcessInformation()
        {
          var proc = HelpActual.HelpCommand.ConfigureProcess("https://aka.ms/dotnet-build");
          Assert.Equal("cmd", proc.StartInfo.FileName);
          Assert.Equal("/c start https://aka.ms/dotnet-build", proc.StartInfo.Arguments);
        }

        [LinuxOnlyFact]
        public void WhenRunOnLinuxDotnetHelpCommandShouldContainProperProcessInformation()
        {
          var proc = HelpActual.HelpCommand.ConfigureProcess("https://aka.ms/dotnet-build");
          Assert.Equal("xdg-open", proc.StartInfo.FileName);
          Assert.Equal("https://aka.ms/dotnet-build", proc.StartInfo.Arguments);

        }
        [MacOsOnlyFact]
        public void WhenRunOnMacOsDotnetHelpCommandShouldContainProperProcessInformation()
        {
          var proc = HelpActual.HelpCommand.ConfigureProcess("https://aka.ms/dotnet-build");
          Assert.Equal("open", proc.StartInfo.FileName);
          Assert.Equal("https://aka.ms/dotnet-build", proc.StartInfo.Arguments);
        }
    }
}
