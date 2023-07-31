// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Install;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallCommandTests
    {
        private const string PackageId = "global.tool.console.demo";

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool install -g --tool-path /tmp/folder {PackageId}");

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var parseResult = Parser.Instance.Parse(
                new[] { "dotnet", "tool", "install", "--local", "--tool-path", "/tmp/folder", PackageId});

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var parseResult = Parser.Instance.Parse(
                new[] { "dotnet", "tool", "install", "-g", "--tool-manifest", "folder/my-manifest.format", "PackageId"});

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var parseResult = Parser.Instance.Parse(
                new[]
                {
                    "dotnet", "tool", "install", "--tool-path", "/tmp/folder", "--tool-manifest", "folder/my-manifest.format", PackageId
                });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithLocalAndFrameworkShowErrorMessage()
        {
            var parseResult = Parser.Instance.Parse(
                new[]
                {
                    "dotnet", "tool", "install", PackageId, "--framework", ToolsetInfo.CurrentTargetFramework
                });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.LocalOptionDoesNotSupportFrameworkOption);
        }
    }
}
