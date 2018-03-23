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
    public class InstallToolParserTests
    {
        private readonly ITestOutputHelper output;

        public InstallToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void InstallGlobaltoolParserCanGetPackageIdAndPackageVersion()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet tool install -g console.test.app --version 1.0.1");

            var parseResult = result["dotnet"]["tool"]["install"];

            var packageId = parseResult.Arguments.Single();
            var packageVersion = parseResult.ValueOrDefault<string>("version");

            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be("1.0.1");
        }

        [Fact]
        public void InstallGlobaltoolParserCanGetFollowingArguments()
        {
            var command = Parser.Instance;
            var result =
                command.Parse(
                    @"dotnet tool install -g console.test.app --version 1.0.1 --framework netcoreapp2.0 --configfile C:\TestAssetLocalNugetFeed");

            var parseResult = result["dotnet"]["tool"]["install"];

            parseResult.ValueOrDefault<string>("configfile").Should().Be(@"C:\TestAssetLocalNugetFeed");
            parseResult.ValueOrDefault<string>("framework").Should().Be("netcoreapp2.0");
        }

        [Fact]
        public void InstallToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool install -g --source-feed {expectedSourceValue} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<string[]>("source-feed").First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void InstallToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Instance.Parse(
                    $"dotnet tool install -g " +
                    $"--source-feed {expectedSourceValue1} " +
                    $"--source-feed {expectedSourceValue2} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];

            appliedOptions.ValueOrDefault<string[]>("source-feed")[0].Should().Be(expectedSourceValue1);
            appliedOptions.ValueOrDefault<string[]>("source-feed")[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void InstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool install -g console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }

        [Fact]
        public void InstallToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet tool install -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.SingleArgumentOrDefault("verbosity").Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void InstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install --tool-path C:\Tools console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\Tools");
        }
    }
}
