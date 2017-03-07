// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
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
        public void An_outgoing_command_line_can_be_generated_based_on_a_parse_result()
        {
            var command = Command("the-command", "",
                Option("-o|--one", "",
                    ZeroOrOneArgument.ForwardAs("/i:{0}")),
                Option("-t|--two", "",
                    ZeroOrOneArgument.ForwardAs("/s:{0}")));

            var result = command.Parse("the-command -t argument-two-value -o 123");

            result["the-command"]
                .ArgsToBeForwarded()
                .Should()
                .BeEquivalentTo("/i:123", "/s:argument-two-value");
        }

        [Fact]
        public void MultipleArgumentsCanBeJoinedWhenForwarding()
        {
            var command = Command("the-command", "",
                Option("-x", "",
                    ZeroOrMoreArguments.ForwardAs(o => $"/x:{string.Join("&", o.Arguments)}")));

            var result = command.Parse("the-command -x one -x two");

            result["the-command"]
                .ArgsToBeForwarded()
                .Should()
                .BeEquivalentTo("/x:one&two");
        }
    }
}