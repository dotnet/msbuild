// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                   .Be(string.Format(LocalizableStrings.OptionRequiresSingleValueWhichIsMissing, "-v"));
        }
    }
}
