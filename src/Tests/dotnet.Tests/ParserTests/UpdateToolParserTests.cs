// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class UpdateInstallToolParserTests
    {
        private readonly ITestOutputHelper _output;

        public UpdateInstallToolParserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void UpdateGlobaltoolParserCanGetPackageId()
        {
            var result = Parser.Instance.Parse("dotnet tool update -g console.test.app");

            var packageId = result.GetValueForArgument<string>(ToolUpdateCommandParser.PackageIdArgument);

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UpdateToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool update -g console.test.app");

            result.GetValueForOption<bool>(ToolUpdateCommandParser.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanGetFollowingArguments()
        {
            var result =
                Parser.Instance.Parse(
                    $@"dotnet tool update -g console.test.app --version 1.0.1 --framework {ToolsetInfo.CurrentTargetFramework} --configfile C:\TestAssetLocalNugetFeed");

            result.GetValueForOption<string>(ToolUpdateCommandParser.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
            result.GetValueForOption<string>(ToolUpdateCommandParser.FrameworkOption).Should().Be(ToolsetInfo.CurrentTargetFramework);
        }

        [Fact]
        public void UpdateToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool update -g --add-source {expectedSourceValue} console.test.app");

            result.GetValueForOption<string[]>(ToolUpdateCommandParser.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void UpdateToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Instance.Parse(
                    $"dotnet tool update -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            result.GetValueForOption<string[]>(ToolUpdateCommandParser.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetValueForOption<string[]>(ToolUpdateCommandParser.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void UpdateToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result =
                Parser.Instance.Parse($"dotnet tool update -g --verbosity:{expectedVerbosityLevel} console.test.app");

            Enum.GetName(result.GetValueForOption<VerbosityOptions>(ToolUpdateCommandParser.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void UpdateToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --tool-path C:\TestAssetLocalNugetFeed console.test.app");

            result.GetValueForOption<string>(ToolUpdateCommandParser.ToolPathOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [Fact]
        public void UpdateToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --no-cache");

            result.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()).Should().ContainSingle("--no-cache");
        }

        [Fact]
        public void UpdateToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --ignore-failed-sources");

            result.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()).Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact]
        public void UpdateToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --disable-parallel");

            result.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()).Should().ContainSingle("--disable-parallel");
        }

        [Fact]
        public void UpdateToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --interactive");

            result.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()).Should().ContainSingle("--interactive");
        }

        [Fact]
        public void UpdateToolParserCanParseVersionOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --version 1.2");

            result.GetValueForOption<string>(ToolUpdateCommandParser.VersionOption).Should().Be("1.2");
        }

        [Fact]
        public void UpdateToolParserCanParseLocalOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --local console.test.app");

            result.GetValueForOption<bool>(ToolUpdateCommandParser.LocalOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --tool-manifest folder/my-manifest.format console.test.app");

            result.GetValueForOption<string>(ToolUpdateCommandParser.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }
    }
}
