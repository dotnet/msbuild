// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
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
            var result = Parser.Instance.Parse("dotnet tool restore --tool-manifest folder/my-manifest.format");

            result.GetValueForOption<string>(ToolRestoreCommandParser.ToolManifestOption).Should().Be("folder/my-manifest.format");
        }

        [Fact]
        public void ToolRestoreParserCanGetFollowingArguments()
        {
            var result =
                Parser.Instance.Parse(
                    @"dotnet tool restore --configfile C:\TestAssetLocalNugetFeed");

            result.GetValueForOption<string>(ToolRestoreCommandParser.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [Fact]
        public void ToolRestoreParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool restore --add-source {expectedSourceValue}");

            result.GetValueForOption<string[]>(ToolRestoreCommandParser.AddSourceOption).First().Should().Be(expectedSourceValue);
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

            result.GetValueForOption<string[]>(ToolRestoreCommandParser.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetValueForOption<string[]>(ToolRestoreCommandParser.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void ToolRestoreParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet tool restore --verbosity {expectedVerbosityLevel}");

            Enum.GetName(result.GetValueForOption<VerbosityOptions>(ToolRestoreCommandParser.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void ToolRestoreParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --no-cache");

            result.OptionValuesToBeForwarded(ToolRestoreCommandParser.GetCommand()).Should().ContainSingle("--no-cache");
        }

        [Fact]
        public void ToolRestoreParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --ignore-failed-sources");
            
            result.OptionValuesToBeForwarded(ToolRestoreCommandParser.GetCommand()).Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact]
        public void ToolRestoreParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --disable-parallel");

            result.OptionValuesToBeForwarded(ToolRestoreCommandParser.GetCommand()).Should().ContainSingle("--disable-parallel");
        }

        [Fact]
        public void ToolRestoreParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool restore --interactive");

            result.OptionValuesToBeForwarded(ToolRestoreCommandParser.GetCommand()).Should().ContainSingle("--interactive");
        }
    }
}
