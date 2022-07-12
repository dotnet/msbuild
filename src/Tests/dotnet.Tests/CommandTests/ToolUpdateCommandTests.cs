// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Update;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Microsoft.NET.TestFramework.Utilities;
using System.CommandLine;
using System.CommandLine.Parsing;
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

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                    "global tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool update --local --tool-path /tmp/folder {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                        "local tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse($"dotnet tool update -g --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
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

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }
    }
}
