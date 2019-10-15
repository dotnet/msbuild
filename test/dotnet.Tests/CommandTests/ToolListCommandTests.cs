// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.List;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolListCommandTests
    {
        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool list -g --tool-path /test/path");
            var appliedCommand = result["dotnet"]["tool"]["list"];
            
            var toolInstallCommand = new ToolListCommand(
                appliedCommand,
                result);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath,
                    "global tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool list --local --tool-path /test/path");
            var appliedCommand = result["dotnet"]["tool"]["list"];

            var toolInstallCommand = new ToolListCommand(
                appliedCommand,
                result);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath,
                        "local tool-path"));
        }
    }
}
