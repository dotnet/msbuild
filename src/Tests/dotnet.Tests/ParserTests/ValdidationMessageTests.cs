// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ValidationMessageTests
    {
        [Fact]
        public void ValidationMessagesFormatCorrectly()
        {
            // Since not all validation messages that we provided to the command-line parser are triggered by our
            // tests (and some may not be possible to trigger with our current usage), unit test  that we can 
            // obtain validation messages through the same interface as the command-line parser.
            //
            // In English configuration, we check that the messages are exactly what we expect and otherwise we at
            // lest ensure that we don't get a FormatException.

            IValidationMessages m = new CommandLineValidationMessages();

            m.CommandAcceptsOnlyOneArgument("xyz", 3)
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Command 'xyz' only accepts a single argument but 3 were provided.");

            m.CommandAcceptsOnlyOneSubcommand("zyx", "a;b;c")
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                "Command 'zyx' only accepts a single subcommand but multiple were provided: a;b;c");

            m.FileDoesNotExist("abc.def")
              .Should().BeVisuallyEquivalentToIfNotLocalized(
                "File does not exist: abc.def");

            m.NoArgumentsAllowed("zzz")
            .Should().BeVisuallyEquivalentToIfNotLocalized(
                "Arguments not allowed for option: zzz");

            m.OptionAcceptsOnlyOneArgument("qqq", 4)
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                "Option 'qqq' only accepts a single argument but 4 were provided.");

            m.RequiredArgumentMissingForCommand("www")
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                "Required argument missing for command: www");

            m.RequiredArgumentMissingForOption("rrr")
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Required argument missing for option: rrr");

            m.RequiredCommandWasNotProvided()
             .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Required command was not provided.");

            m.UnrecognizedArgument("apple", new[] { "banana", "orange" })
              .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Argument 'apple' not recognized. Must be one of: \n\t'banana'\n\t\'orange'");

            m.UnrecognizedCommandOrArgument("ppp")
              .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Unrecognized command or argument 'ppp'");

            m.UnrecognizedOption("apple", new[] { "banana", "orange" })
              .Should().BeVisuallyEquivalentToIfNotLocalized(
                 "Option 'apple' not recognized. Must be one of: \n\t'banana'\n\t\'orange'");
        }
    }
}
