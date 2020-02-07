// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.Runtime.InteropServices;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallCommandTests
    {
        private const string PackageId = "global.tool.console.demo";

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool install -g --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(
                "dotnet tool",
                new[] {"install", "-g", "--tool-path", "/tmp/folder", PackageId});

            var toolInstallCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                    "global tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool install --local --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(
                "dotnet tool",
                new[] {"install", "--local", "--tool-path", "/tmp/folder", PackageId});

            var toolInstallCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        "local tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse($"dotnet tool install -g --tool-manifest folder/my-manifest.format {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(
                "dotnet tool",
                new[] {"install", "-g", "--tool-manifest", "folder/my-manifest.format", "PackageId"});

            var toolInstallCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse(
                    $"dotnet tool install --tool-path /tmp/folder --tool-manifest folder/my-manifest.format {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(
                "dotnet tool",
                new[]
                {
                    "install", "--tool-path", "/tmp/folder", "--tool-manifest", "folder/my-manifest.format", PackageId
                });

            var toolInstallCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithLocalAndFrameworkShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse(
                    $"dotnet tool install {PackageId} --framework netcoreapp2.1");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(
                "dotnet tool",
                new[]
                {
                    "install", PackageId, "--framework", "netcoreapp2.1"
                });

            var toolInstallCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.LocalOptionDoesNotSupportFrameworkOption);
        }
    }
}
