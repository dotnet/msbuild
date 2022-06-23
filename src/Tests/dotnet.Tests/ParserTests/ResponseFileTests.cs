// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ResponseFileTests
    {
        [Fact]
        public void Can_safely_expand_response_file_lines() {
            var tempFilePath = Path.GetTempFileName();
            var lines = new[] {
                "build",
                "a b",
                "/p=\".;c:/program files/\""
            };
            File.WriteAllLines(tempFilePath, lines);

            var quoted = lines.Select(l => $"\"{l}\"");
            var parser = Parser.Instance;
            var parseResult = parser.Parse(new []{ "dotnet", $"@{tempFilePath}" });
            var tokens = parseResult.Tokens.Select(t => t.Value);
            tokens.Should().HaveCount(lines.Length + 1); // dotnet token too
            tokens.Should().StartWith("dotnet");
            tokens.Skip(1).Should().BeEquivalentTo(quoted);
        }
    }
}
     