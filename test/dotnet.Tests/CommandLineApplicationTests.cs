using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class CommandLineApplicationTests
    {
        [Fact]
        public void WhenAnOptionRequiresASingleValueThatIsNotSuppliedItThrowsCommandParsingException()
        {
            var app = new CommandLineApplication();

            app.Option("-v|--verbosity", "be verbose", CommandOptionType.SingleValue);

            Action execute = () => app.Execute("-v");

            execute.ShouldThrow<CommandParsingException>()
                   .Which
                   .Message
                   .Should()
                   .Be("Required value for option '-v' was not provided.");
        }
    }
}
