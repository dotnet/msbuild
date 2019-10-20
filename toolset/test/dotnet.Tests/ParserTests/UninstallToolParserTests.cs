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
    public class UninstallToolParserTests
    {
        private readonly ITestOutputHelper output;

        public UninstallToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void UninstallToolParserCanGetPackageId()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet tool uninstall -g console.test.app");

            var parseResult = result["dotnet"]["tool"]["uninstall"];

            var packageId = parseResult.Arguments.Single();

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UninstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool uninstall -g console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["uninstall"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }

        [Fact]
        public void UninstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-path C:\Tools console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["uninstall"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\Tools");
        }
        
        [Fact]
        public void UninstallToolParserCanParseLocalOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --local console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["uninstall"];
            appliedOptions.ValueOrDefault<bool>("local").Should().Be(true);
        }
        
        [Fact]
        public void UninstallToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-manifest folder/my-manifest.format console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["uninstall"];
            appliedOptions.SingleArgumentOrDefault("tool-manifest").Should().Be(@"folder/my-manifest.format");
        }
    }
}
