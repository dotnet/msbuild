// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using HelpActual = Microsoft.DotNet.Tools.Help;

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetHelpCommand : TestBase
    {
        private const string HelpText =
@"Usage: dotnet help [options] <COMMAND_NAME>

Arguments:
  <COMMAND_NAME>   The SDK command to launch online help for.

Options:
  -h, --help   Show command line help.";

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedToDotnetHelpCommandItPrintsUsage(string helpArg)
        {
            var cmd = new HelpCommand()
                .ExecuteWithCapturedOutput($"{helpArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }
    }
}
