// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.TestFramework;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ResponseFileTests : SdkTest
    {
        public ResponseFileTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Can_safely_expand_response_file_lines() {
            var tempFileDir = _testAssetsManager.CreateTestDirectory().Path;
            var tempFilePath = Path.Combine(tempFileDir, "params.rsp");
            var lines = new[] {
                "build",
                "a b",
                "-p:VSTestTestAdapterPath=\".;/opt/buildagent/plugins/dotnet/tools/vstest15\""
            };
            File.WriteAllLines(tempFilePath, lines);

            var parser = Parser.Instance;
            var parseResult = parser.Parse(new []{ "dotnet", $"@{tempFilePath}" });

            var tokens = parseResult.Tokens.Select(t => t.Value);
            var tokenString = string.Join(", ", tokens);
            var bc = Microsoft.DotNet.Tools.Build.BuildCommand.FromParseResult(parseResult);
            var tokenized = new [] {
                "build",
                "a b",
                "-p",
                "VSTestTestAdapterPath=\".;/opt/buildagent/plugins/dotnet/tools/vstest15\""
            };

            Log.WriteLine($"MSbuild Args are {string.Join(" ", bc.MSBuildArguments)}");
            Log.WriteLine($"Parse Diagram is {parseResult.Diagram()}");
            Log.WriteLine($"Token string is {tokenString}");
            tokens.Skip(1).Should().BeEquivalentTo(tokenized);
        }

        [Fact]
        public void Can_skip_empty_and_commented_lines() {
             var tempFileDir = _testAssetsManager.CreateTestDirectory().Path;
            var tempFilePath = Path.Combine(tempFileDir, "skips.rsp");
            var lines = new[] {
                "build",
                "",
                "run# but skip this",
                "# and this"
            };
            File.WriteAllLines(tempFilePath, lines);

            var parser = Parser.Instance;
            var parseResult = parser.Parse(new []{ "dotnet", $"@{tempFilePath}" });
            var tokens = parseResult.Tokens.Select(t => t.Value);
            var tokenized = new [] {
                "build",
                "run"
            };

            tokens.Skip(1).Should().BeEquivalentTo(tokenized);
        }
    }
}
