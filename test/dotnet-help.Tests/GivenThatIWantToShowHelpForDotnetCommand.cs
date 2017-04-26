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
@"Usage: dotnet [host-options] [command] [arguments] [common-options]

Arguments:
  [command]             The command to execute
  [arguments]           Arguments to pass to the command
  [host-options]        Options specific to dotnet (host)
  [common-options]      Options common to all commands

Common options:
  -v|--verbose          Enable verbose output
  -h|--help             Show help

Host options (passed before the command):
  -d|--diagnostics      Enable diagnostic output
  --version             Display .NET CLI Version Number
  --info                Display .NET CLI Info

Commands:
  new           Initialize .NET projects.
  restore       Restore dependencies specified in the .NET project.
  build         Builds a .NET project.
  publish       Publishes a .NET project for deployment (including the runtime).
  run           Compiles and immediately executes a .NET project.
  test          Runs unit tests using the test runner specified in the project.
  pack          Creates a NuGet package.
  migrate       Migrates a project.json based project to a msbuild based project.
  clean         Clean build output(s).
  sln           Modify solution (SLN) files.

Project modification commands:
  add           Add items to the project.
  remove        Remove items from the project.
  list          List items in the project.

Advanced Commands:
  nuget         Provides additional NuGet commands.
  msbuild       Runs Microsoft Build Engine (MSBuild).
  vstest        Runs Microsoft Test Execution Command Line Tool.";

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
            cmd.StdOut.Should().ContainVisuallySameFragment(HelpText);
        }

        [Fact]
        public void WhenHelpCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new HelpCommand()
                .ExecuteWithCapturedOutput();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragment(HelpText);
        }

        [Fact]
        public void WhenInvalidCommandIsPassedToDotnetHelpItPrintsError()
        {
          var cmd = new DotnetCommand()
                .ExecuteWithCapturedOutput("help invalid");

          cmd.Should().Fail();
          cmd.StdErr.Should().ContainVisuallySameFragment($"Specified command 'invalid' is not a valid CLI command. Please specify a valid CLI commands. For more information, run dotnet help.");
          cmd.StdOut.Should().ContainVisuallySameFragment(HelpText);
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
