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

            var appliedOptions = result["dotnet"]["tool"]["list"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanGetLocalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool list --local");

            var appliedOptions = result["dotnet"]["tool"]["list"];
            appliedOptions.ValueOrDefault<bool>("local").Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool list --tool-path C:\Tools ");

            var appliedOptions = result["dotnet"]["tool"]["list"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\Tools");
        }
    }
}
