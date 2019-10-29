// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ListPackageParserTests
    {
        private readonly ITestOutputHelper output;

        public ListPackageParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ListPackageCanForwardInteractiveFlag()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet list package --interactive");

            var appliedOptions = result["dotnet"]["list"]["package"];

            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--interactive");
        }
    }
}
