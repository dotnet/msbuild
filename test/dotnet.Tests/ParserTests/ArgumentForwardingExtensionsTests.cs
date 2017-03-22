// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;
using static Microsoft.DotNet.Cli.CommandLine.Accept;
using static Microsoft.DotNet.Cli.CommandLine.Create;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ArgumentForwardingExtensionsTests
    {
        [Fact]
        public void AnOutgoingCommandLineCanBeGeneratedBasedOnAParseResult()
        {
            var command = Command("the-command", "",
                                  Option("-o|--one", "",
                                         ZeroOrOneArgument()
                                             .ForwardAsSingle(o => $"/i:{o.Arguments.Single()}")),
                                  Option("-t|--two", "",
                                         NoArguments()
                                             .ForwardAs("/s:true")));

            var result = command.Parse("the-command -t -o 123");

            result["the-command"]
                .OptionValuesToBeForwarded()
                .Should()
                .BeEquivalentTo("/i:123", "/s:true");
        }

        [Fact]
        public void MultipleArgumentsCanBeJoinedWhenForwarding()
        {
            var command = Command("the-command", "",
                                  Option("-x", "",
                                         ZeroOrMoreArguments()
                                             .ForwardAsSingle(o => $"/x:{string.Join("&", o.Arguments)}")));

            var result = command.Parse("the-command -x one -x two");

            result["the-command"]
                .OptionValuesToBeForwarded()
                .Should()
                .BeEquivalentTo("/x:one&two");
        }

        [Fact]
        public void AnArgumentCanBeForwardedAsIs()
        {
            var command = Command("the-command", "",
                                  Option("-x", "",
                                         ZeroOrMoreArguments()
                                             .Forward()));

            var result = command.Parse("the-command -x one");

            result["the-command"]
                .OptionValuesToBeForwarded()
                .Should()
                .BeEquivalentTo("one");
        }
    }
}
