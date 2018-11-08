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
using Newtonsoft.Json;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.Runtime.InteropServices;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands
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
            var parseResult = parser.ParseFrom("dotnet tool", new[] {"install", "-g", PackageId});

            var installGlobalOrToolPathCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => installGlobalOrToolPathCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.InstallToolCommandInvalidGlobalAndToolPath);
        }

        [Fact]
        public void WhenRunWithNeitherOfGlobalNorToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool install {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet tool", new[] { "install", "-g", PackageId });

            var installGlobalOrToolPathCommand = new ToolInstallCommand(
                appliedCommand,
                parseResult);

            Action a = () => installGlobalOrToolPathCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.InstallToolCommandNeedGlobalOrToolPath);
        }
    }
}
