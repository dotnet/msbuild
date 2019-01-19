// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
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

            var appliedOptions = result["dotnet"]["tool"]["run"];
            var packageId = appliedOptions.Arguments.Single();

            packageId.Should().Be("dotnetsay");
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnmatchedTokens()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay hi");
            result.UnmatchedTokens.Should().Contain("hi");
            result.UnparsedTokens.Should().BeEmpty();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay -- hi");
            result.UnmatchedTokens.Should().BeEmpty();
            result.UnparsedTokens.Should().Contain("hi");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens2()
        {
            var result = Parser.Instance.Parse("dotnet tool run dotnetsay hi1 -- hi2");
            result.UnmatchedTokens.Should().Contain("hi1");
            result.UnparsedTokens.Should().Contain("hi2");
            result.Errors.Should().BeEmpty();
        }
    }
}
