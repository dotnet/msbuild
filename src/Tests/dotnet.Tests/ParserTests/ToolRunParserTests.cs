// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ToolRunParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolRunParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ListToolParserCanGetToolCommandNameArgument()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay");

            var packageId = result.GetValueForArgument<string>(ToolRunCommandParser.CommandNameArgument);

            packageId.Should().Be("dotnetsay");
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnmatchedTokens()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay hi");

            result.ShowHelpOrErrorIfAppropriate(); // Should not throw error
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay -- hi");

            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens2()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay hi1 -- hi2");

            result.ShowHelpOrErrorIfAppropriate(); // Should not throw error
        }

        [Fact]
        public void RootSubCommandIsToolCommand()
        {
            var result = Parser.Instance.Parse("dotnetsay run -v arg");
            result.RootSubCommandResult().Should().Be("dotnetsay");
        }
    }
}
