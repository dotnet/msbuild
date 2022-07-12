// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Search;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ToolSearchParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolSearchParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void DotnetToolSearchShouldThrowWhenNoSearchTerm()
        {
            var result = Parser.Instance.Parse("dotnet tool search");
            Action a = () => new ToolSearchCommand(result);
            a.ShouldThrow<CommandParsingException>();
        }

        [Fact]
        public void ListSearchParserCanGetArguments()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var packageId = result.GetValueForArgument<string>(ToolSearchCommandParser.SearchTermArgument);

            packageId.Should().Be("mytool");
            result.UnmatchedTokens.Should().BeEmpty();
            result.GetValueForOption<bool>(ToolSearchCommandParser.DetailOption).Should().Be(true);
            result.GetValueForOption<string>(ToolSearchCommandParser.SkipOption).Should().Be("3");
            result.GetValueForOption<string>(ToolSearchCommandParser.TakeOption).Should().Be("4");
            result.GetValueForOption<bool>(ToolSearchCommandParser.PrereleaseOption).Should().Be(true);
        }
    }
}
