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
    public class ToolRestoreParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolRestoreParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ToolRestoreParserCanGetManifestFilePath()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet tool restore --tool-manifest folder/my-manifest.format");

            var parseResult = result["dotnet"]["tool"]["restore"];

            parseResult.ValueOrDefault<string>("tool-manifest").Should().Be("folder/my-manifest.format");
        }

        [Fact]
        public void ToolRestoreParserCanGetFollowingArguments()
        {
            var command = Parser.Instance;
            var result =
                command.Parse(
                    @"dotnet tool restore --configfile C:\TestAssetLocalNugetFeed");

            var parseResult = result["dotnet"]["tool"]["restore"];

            parseResult.ValueOrDefault<string>("configfile").Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [Fact]
        public void ToolRestoreParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool restore --add-source {expectedSourceValue}");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.ValueOrDefault<string[]>("add-source").First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void ToolRestoreParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Instance.Parse(
                    $"dotnet tool restore " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2}");

            var appliedOptions = result["dotnet"]["tool"]["restore"];

            appliedOptions.ValueOrDefault<string[]>("add-source")[0].Should().Be(expectedSourceValue1);
            appliedOptions.ValueOrDefault<string[]>("add-source")[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void ToolRestoreParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet tool restore --verbosity {expectedVerbosityLevel}");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.SingleArgumentOrDefault("verbosity").Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void ToolRestoreParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --no-cache");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--no-cache");
        }

        [Fact]
        public void ToolRestoreParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --ignore-failed-sources");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact]
        public void ToolRestoreParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --disable-parallel");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--disable-parallel");
        }

        [Fact]
        public void ToolRestoreParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --interactive");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--interactive");
        }

        [Fact(Skip = "pending")]
        public void ToolRestoreParserHasOptionalLocal()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --local");

            var appliedOptions = result["dotnet"]["tool"]["restore"];
            appliedOptions.ValueOrDefault<bool>("local").Should().Be(true);
            
            var result2 =
                Parser.Instance.Parse(@"dotnet tool restore");

            var appliedOptions2 = result2["dotnet"]["tool"]["restore"];
            appliedOptions2.ValueOrDefault<bool>("local").Should().Be(true);
        }
    }
}
