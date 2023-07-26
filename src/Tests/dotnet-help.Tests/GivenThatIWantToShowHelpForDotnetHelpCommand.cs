// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetHelpCommand : SdkTest
    {
        private const string HelpText =
@"Description:
  .NET CLI help utility

Usage:
  dotnet help [<COMMAND_NAME>] [options]

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
