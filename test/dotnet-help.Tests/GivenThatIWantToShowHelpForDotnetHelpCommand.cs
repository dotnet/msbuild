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
  <COMMAND_NAME>   CLI command for which to view more detailed help.

Options:
  -h, --help   Show help information";

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
