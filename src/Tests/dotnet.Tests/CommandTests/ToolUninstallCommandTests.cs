// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUninstallCommandTests
    {
        private readonly BufferedReporter _reporter;

        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        

        public ToolUninstallCommandTests()
        {
            _reporter = new BufferedReporter();
        }
        
        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool uninstall -g --tool-path /tmp/folder {PackageId}");
            
            var toolUninstallCommand = new ToolUninstallCommand(result);

            Action a = () => toolUninstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool uninstall --local --tool-path /tmp/folder {PackageId}");

            var toolUninstallCommand = new ToolUninstallCommand(result);

            Action a = () => toolUninstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }
        
        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool uninstall -g --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUninstallCommand = new ToolUninstallCommand(result);
            
            Action a = () => toolUninstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result = Parser.Instance.Parse(
                    $"dotnet tool uninstall --tool-path /tmp/folder --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUninstallCommand = new ToolUninstallCommand(result);

            Action a = () => toolUninstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }
    }
}
