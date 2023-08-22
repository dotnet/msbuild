// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
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

            var packageId = result.GetValue<string>(ToolUninstallCommandParser.PackageIdArgument);

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UninstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool uninstall -g console.test.app");

            result.GetValue<bool>(ToolUninstallCommandParser.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void UninstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-path C:\Tools console.test.app");

            result.GetValue<string>(ToolUninstallCommandParser.ToolPathOption).Should().Be(@"C:\Tools");
        }

        [Fact]
        public void UninstallToolParserCanParseLocalOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --local console.test.app");

            result.GetValue<bool>(ToolUninstallCommandParser.LocalOption).Should().Be(true);
        }

        [Fact]
        public void UninstallToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool uninstall --tool-manifest folder/my-manifest.format console.test.app");

            result.GetValue<string>(ToolUninstallCommandParser.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }
    }
}
