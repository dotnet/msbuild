// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Search;
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
            a.Should().Throw<CommandParsingException>();
        }

        [Fact]
        public void ListSearchParserCanGetArguments()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var packageId = result.GetValue<string>(ToolSearchCommandParser.SearchTermArgument);

            packageId.Should().Be("mytool");
            result.UnmatchedTokens.Should().BeEmpty();
            result.GetValue<bool>(ToolSearchCommandParser.DetailOption).Should().Be(true);
            result.GetValue<string>(ToolSearchCommandParser.SkipOption).Should().Be("3");
            result.GetValue<string>(ToolSearchCommandParser.TakeOption).Should().Be("4");
            result.GetValue<bool>(ToolSearchCommandParser.PrereleaseOption).Should().Be(true);
        }
    }
}
