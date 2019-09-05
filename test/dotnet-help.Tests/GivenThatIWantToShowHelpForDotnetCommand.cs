// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using HelpActual = Microsoft.DotNet.Tools.Help;

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetCommand : TestBase
    {
        private const string HelpText =
@"Usage: dotnet [runtime-options] [path-to-application] [arguments]

Execute a .NET Core application.

runtime-options:
  --additionalprobingpath <path>   Path containing probing policy and assemblies to probe for.
  --additional-deps <path>         Path to additional deps.json file.
  --depsfile                       Path to <application>.deps.json file.
  --fx-version <version>           Version of the installed Shared Framework to use to run the application.
  --roll-forward <setting>         Roll forward to framework version  (LatestPatch, Minor, LatestMinor, Major, LatestMajor, Disable).
  --runtimeconfig                  Path to <application>.runtimeconfig.json file.

path-to-application:
  The path to an application .dll file to execute.

Usage: dotnet [sdk-options] [command] [command-options] [arguments]

Execute a .NET Core SDK command.

sdk-options:
  -d|--diagnostics  Enable diagnostic output.
  -h|--help         Show command line help.
  --info            Display .NET Core information.
  --list-runtimes   Display the installed runtimes.
  --list-sdks       Display the installed SDKs.
  --version         Display .NET Core SDK version in use.

SDK commands:
  add               Add a package or reference to a .NET project.
  build             Build a .NET project.
  build-server      Interact with servers started by a build.
  clean             Clean build outputs of a .NET project.
  help              Show command line help.
  list              List project references of a .NET project.
  msbuild           Run Microsoft Build Engine (MSBuild) commands.
  new               Create a new .NET project or file.
  nuget             Provides additional NuGet commands.
  pack              Create a NuGet package.
  publish           Publish a .NET project for deployment.
  remove            Remove a package or reference from a .NET project.
  restore           Restore dependencies specified in a .NET project.
  run               Build and run a .NET project output.
  sln               Modify Visual Studio solution files.
  store             Store the specified assemblies in the runtime package store.
  test              Run unit tests using the test runner specified in a .NET project.
  tool              Install or manage tools that extend the .NET experience.
  vstest            Run Microsoft Test Engine (VSTest) commands.

Additional commands from bundled tools:
  dev-certs         Create and manage development certificates.
  fsi               Start F# Interactive / execute F# scripts.
  sql-cache         SQL Server cache command-line tools.
  user-secrets      Manage development user secrets.
  watch             Start a file watcher that runs a command when files change.

Run 'dotnet [command] --help' for more information on a command.";

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
