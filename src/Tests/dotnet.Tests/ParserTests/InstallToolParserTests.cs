// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
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
            var result = Parser.Instance.Parse("dotnet tool install -g console.test.app --version 1.0.1");

            var packageId = result.GetValue<string>(ToolInstallCommandParser.PackageIdArgument);
            var packageVersion = result.GetValue<string>(ToolInstallCommandParser.VersionOption);

            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be("1.0.1");
        }

        [Fact]
        public void InstallGlobaltoolParserCanGetFollowingArguments()
        {
            var result =
                Parser.Instance.Parse(
                    $@"dotnet tool install -g console.test.app --version 1.0.1 --framework {ToolsetInfo.CurrentTargetFramework} --configfile C:\TestAssetLocalNugetFeed");

            result.GetValue<string>(ToolInstallCommandParser.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
            result.GetValue<string>(ToolInstallCommandParser.FrameworkOption).Should().Be(ToolsetInfo.CurrentTargetFramework);
        }

        [Fact]
        public void InstallToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool install -g --add-source {expectedSourceValue} console.test.app");

            result.GetValue<string[]>(ToolInstallCommandParser.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void InstallToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Instance.Parse(
                    $"dotnet tool install -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            
            result.GetValue<string[]>(ToolInstallCommandParser.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetValue<string[]>(ToolInstallCommandParser.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void InstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool install -g console.test.app");

            result.GetValue(ToolInstallCommandParser.GlobalOption).Should().Be(true);
        }
        
        [Fact]
        public void InstallToolParserCanGetLocalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool install --local console.test.app");

            result.GetValue(ToolInstallCommandParser.LocalOption).Should().Be(true);
        }

        [Fact]
        public void InstallToolParserCanGetManifestOption()
        {
            var result =
                Parser.Instance.Parse(
                    "dotnet tool install --local console.test.app --tool-manifest folder/my-manifest.format");

            result.GetValue(ToolInstallCommandParser.ToolManifestOption).Should().Be("folder/my-manifest.format");
        }

        [Fact]
        public void InstallToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet tool install -g --verbosity:{expectedVerbosityLevel} console.test.app");

            Enum.GetName(result.GetValue<VerbosityOptions>(ToolInstallCommandParser.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void InstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install --tool-path C:\Tools console.test.app");

            result.GetValue(ToolInstallCommandParser.ToolPathOption).Should().Be(@"C:\Tools");
        }

        [Fact]
        public void InstallToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --no-cache");

            result.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand()).Should().ContainSingle("--no-cache");
        }

        [Fact]
        public void InstallToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --ignore-failed-sources");

            result.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand()).Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact]
        public void InstallToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --disable-parallel");

            result.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand()).Should().ContainSingle("--disable-parallel");
        }

        [Fact]
        public void InstallToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --interactive");

            result.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand()).Should().ContainSingle("--interactive");
        }
    }
}
