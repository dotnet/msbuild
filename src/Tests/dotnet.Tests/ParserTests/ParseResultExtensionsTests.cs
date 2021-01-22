// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ParseResultExtensionsTests
    {
        private readonly ITestOutputHelper output;

        public ParseResultExtensionsTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData("build /p:prop=true", "build")]
        [InlineData("add package", "add")]
        [InlineData("watch run", "watch")]
        [InlineData("watch run -h", "watch")]
        [InlineData("ignore list", "ignore")] // Global tool
        public void RootSubCommandResultReturnsCorrectSubCommand(string input, string expected)
        {
            var result = Parser.Instance.Parse(input);

            result.RootSubCommandResult()
                .Should()
                .Be(expected);
        }
    }
}
