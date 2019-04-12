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
            var command = Parser.Instance;
            var result = command.Parse("dotnet tool update -g console.test.app");

            var parseResult = result["dotnet"]["tool"]["update"];

            var packageId = parseResult.Arguments.Single();

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UpdateToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool update -g console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanGetFollowingArguments()
        {
            var command = Parser.Instance;
            var result =
                command.Parse(
                    @"dotnet tool update -g console.test.app --version 1.0.1 --framework netcoreapp2.0 --configfile C:\TestAssetLocalNugetFeed");

            var parseResult = result["dotnet"]["tool"]["update"];

            parseResult.ValueOrDefault<string>("configfile").Should().Be(@"C:\TestAssetLocalNugetFeed");
            parseResult.ValueOrDefault<string>("framework").Should().Be("netcoreapp2.0");
        }

        [Fact]
        public void UpdateToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool update -g --add-source {expectedSourceValue} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.ValueOrDefault<string[]>("add-source").First().Should().Be(expectedSourceValue);
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

            var appliedOptions = result["dotnet"]["tool"]["update"];

            appliedOptions.ValueOrDefault<string[]>("add-source")[0].Should().Be(expectedSourceValue1);
            appliedOptions.ValueOrDefault<string[]>("add-source")[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void UpdateToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result =
                Parser.Instance.Parse($"dotnet tool update -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.SingleArgumentOrDefault("verbosity").Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void UpdateToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --tool-path C:\TestAssetLocalNugetFeed console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [Fact]
        public void UpdateToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --no-cache");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--no-cache");
        }

        [Fact]
        public void UpdateToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --ignore-failed-sources");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact]
        public void UpdateToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --disable-parallel");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--disable-parallel");
        }

        [Fact]
        public void UpdateToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --interactive");

            var appliedOptions = result["dotnet"]["tool"]["update"];

            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--interactive");
        }

        [Fact]
        public void UpdateToolParserCanParseVersionOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update -g console.test.app --version 1.2");

            var appliedOptions = result["dotnet"]["tool"]["update"];

            appliedOptions.SingleArgumentOrDefault("version").Should().Be("1.2");
        }

        [Fact]
        public void UpdateToolParserCanParseLocalOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --local console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.ValueOrDefault<bool>("local").Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool update --tool-manifest folder/my-manifest.format console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["update"];
            appliedOptions.SingleArgumentOrDefault("tool-manifest").Should().Be(@"folder/my-manifest.format");
        }
    }
}
