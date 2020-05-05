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
    public class ToolSearchParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolSearchParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ListSearchParserCanGetArguments()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var appliedOptions = result["dotnet"]["tool"]["search"];
            var packageId = appliedOptions.Arguments.Single();

            packageId.Should().Be("mytool");
            result.UnmatchedTokens.Should().BeEmpty();
            appliedOptions.ValueOrDefault<bool>("detail").Should().Be(true);
            appliedOptions.ValueOrDefault<string>("skip").Should().Be("3");
            appliedOptions.ValueOrDefault<string>("take").Should().Be("4");
            appliedOptions.ValueOrDefault<bool>("prerelease").Should().Be(true);
        }
    }
}
