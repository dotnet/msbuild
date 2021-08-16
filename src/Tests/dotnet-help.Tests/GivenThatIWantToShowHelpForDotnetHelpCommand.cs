// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetHelpCommand : SdkTest
    {
        private const string HelpText =
@"Description:
  .NET CLI help utility

Usage:
  dotnet [options] help [<COMMAND_NAME>]

Arguments:
  <COMMAND_NAME>  The SDK command to launch online help for.

Options:
  -?, -h, --help  Show command line help.";

        public GivenThatIWantToShowHelpForDotnetHelpCommand(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedToDotnetHelpCommandItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log, "help")
                .Execute($"{helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }
    }
}
