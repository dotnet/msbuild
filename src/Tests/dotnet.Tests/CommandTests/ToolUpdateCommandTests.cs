// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Update;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateCommandTests
    {
        private readonly BufferedReporter _reporter;
        private const string PackageId = "global.tool.console.demo";


        public ToolUpdateCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool update -g --tool-path /tmp/folder {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool update --local --tool-path /tmp/folder {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse($"dotnet tool update -g --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse(
                    $"dotnet tool update --tool-path /tmp/folder --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }
    }
}
