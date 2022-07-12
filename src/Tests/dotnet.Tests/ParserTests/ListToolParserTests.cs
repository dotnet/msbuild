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
    public class ListToolParserTests
    {
        private readonly ITestOutputHelper output;

        public ListToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ListToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool list -g");

            result.GetValueForOption<bool>(ToolListCommandParser.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanGetLocalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool list --local");

            result.GetValueForOption<bool>(ToolListCommandParser.LocalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool list --tool-path C:\Tools ");

            result.GetValueForOption<string>(ToolListCommandParser.ToolPathOption).Should().Be(@"C:\Tools");
        }
    }
}
