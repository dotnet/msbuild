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
            var result = command.Parse("dotnet install tool -g console.test.app --version 1.0.1");

            var parseResult = result["dotnet"]["install"]["tool"];

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
                    @"dotnet install tool -g console.test.app --version 1.0.1 --framework netcoreapp2.0 --configfile C:\TestAssetLocalNugetFeed");

            var parseResult = result["dotnet"]["install"]["tool"];

            parseResult.ValueOrDefault<string>("configfile").Should().Be(@"C:\TestAssetLocalNugetFeed");
            parseResult.ValueOrDefault<string>("framework").Should().Be("netcoreapp2.0");
        }

        [Fact]
        public void InstallToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result = Parser.Instance.Parse($"dotnet install tool -g --source {expectedSourceValue} console.test.app");

            var appliedOptions = result["dotnet"]["install"]["tool"];
            appliedOptions.ValueOrDefault<string>("source").Should().Be(expectedSourceValue);
        }

        [Fact]
        public void InstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet install tool -g console.test.app");

            var appliedOptions = result["dotnet"]["install"]["tool"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }

        [Fact]
        public void InstallToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet install tool -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var appliedOptions = result["dotnet"]["install"]["tool"];
            appliedOptions.SingleArgumentOrDefault("verbosity").Should().Be(expectedVerbosityLevel);
        }
        
        [Fact]
        public void InstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet install tool --tool-path C:\TestAssetLocalNugetFeed console.test.app");

            var appliedOptions = result["dotnet"]["install"]["tool"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\TestAssetLocalNugetFeed");
        }
    }
}
