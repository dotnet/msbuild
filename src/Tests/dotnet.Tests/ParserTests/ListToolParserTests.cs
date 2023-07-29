// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
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

            result.GetValue<bool>(ToolListCommandParser.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanGetLocalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool list --local");

            result.GetValue<bool>(ToolListCommandParser.LocalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool list --tool-path C:\Tools ");

            result.GetValue<string>(ToolListCommandParser.ToolPathOption).Should().Be(@"C:\Tools");
        }
    }
}
