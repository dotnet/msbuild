// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;
using InstallLocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using Microsoft.DotNet.ShellShim;

namespace Microsoft.DotNet.Tests.Commands
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
            var appliedCommand = result["dotnet"]["tool"]["uninstall"];
            
            var toolUninstallCommand = new ToolUninstallCommand(
                appliedCommand,
                result);

            Action a = () => toolUninstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath,
                    "global tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool uninstall --local --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["uninstall"];

            var toolUninstallCommand = new ToolUninstallCommand(
                appliedCommand,
                result);

            Action a = () => toolUninstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        "local tool-path"));
        }
        
        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse($"dotnet tool uninstall -g --tool-manifest folder/my-manifest.format {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["uninstall"];

            var toolUninstallCommand = new ToolUninstallCommand(
                appliedCommand,
                result);
            
            Action a = () => toolUninstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse(
                    $"dotnet tool uninstall --tool-path /tmp/folder --tool-manifest folder/my-manifest.format {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["uninstall"];

            var toolUninstallCommand = new ToolUninstallCommand(
                appliedCommand,
                result);

            Action a = () => toolUninstallCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }
    }
}
