// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
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
            var result = Parser.Instance.Parse("dotnet tool uninstall -g console.test.app");

            var packageId = result.GetValueForArgument<string>(ToolUninstallCommandParser.PackageIdArgument);

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UninstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool uninstall -g console.test.app");

            result.GetValueForOption<bool>(ToolUninstallCommandParser.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void UninstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-path C:\Tools console.test.app");

            result.GetValueForOption<string>(ToolUninstallCommandParser.ToolPathOption).Should().Be(@"C:\Tools");
        }
        
        [Fact]
        public void UninstallToolParserCanParseLocalOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --local console.test.app");

            result.GetValueForOption<bool>(ToolUninstallCommandParser.LocalOption).Should().Be(true);
        }
        
        [Fact]
        public void UninstallToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-manifest folder/my-manifest.format console.test.app");

            result.GetValueForOption<string>(ToolUninstallCommandParser.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }
    }
}
