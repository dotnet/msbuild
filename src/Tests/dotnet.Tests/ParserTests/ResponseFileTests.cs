// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ResponseFileTests : SdkTest
    {
        public ResponseFileTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Can_safely_expand_response_file_lines()
        {
            var tempFileDir = _testAssetsManager.CreateTestDirectory().Path;
            var tempFilePath = Path.Combine(tempFileDir, "params.rsp");
            var lines = new[] {
                "build",
                "a b",
                "-p:VSTestTestAdapterPath=\".;/opt/buildagent/plugins/dotnet/tools/vstest15\""
            };
            File.WriteAllLines(tempFilePath, lines);

            var parser = Parser.Instance;
            var parseResult = parser.Parse(new[] { "dotnet", $"@{tempFilePath}" });

            var tokens = parseResult.Tokens.Select(t => t.Value);
            var tokenString = string.Join(", ", tokens);
            var bc = Tools.Build.BuildCommand.FromParseResult(parseResult);
            var tokenized = new[] {
                "build",
                "a b",
                "-p",
                "VSTestTestAdapterPath=\".;/opt/buildagent/plugins/dotnet/tools/vstest15\""
            };

            Log.WriteLine($"MSbuild Args are {string.Join(" ", bc.MSBuildArguments)}");
            Log.WriteLine($"Parse Diagram is {parseResult.ToString()}");
            Log.WriteLine($"Token string is {tokenString}");
            tokens.Skip(1).Should().BeEquivalentTo(tokenized);
        }

        [Fact]
        public void Can_skip_empty_and_commented_lines()
        {
            var tempFileDir = _testAssetsManager.CreateTestDirectory().Path;
            var tempFilePath = Path.Combine(tempFileDir, "skips.rsp");
            var lines = new[] {
                "build",
                "",
                "  #skip this",
                "run #but don't skip this",
                "# and this"
            };
            File.WriteAllLines(tempFilePath, lines);

            var parser = Parser.Instance;
            var parseResult = parser.Parse(new[] { "dotnet", $"@{tempFilePath}" });
            var tokens = parseResult.Tokens.Select(t => t.Value);
            var tokenized = new[] {
                "build",
                "run #but don't skip this"
            };

            tokens.Skip(1).Should().BeEquivalentTo(tokenized);
        }
    }
}
