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
using Microsoft.DotNet.Tools.Install.Tool;
using Microsoft.DotNet.Tools.Uninstall.Tool;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.Uninstall.Tool.LocalizableStrings;
using InstallLocalizableStrings = Microsoft.DotNet.Tools.Install.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands
{
    public class UninstallToolCommandTests
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly ShellShimRepositoryMock _shellShimRepositoryMock;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;

        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private const string ShimsDirectory = "shims";
        private const string ToolsDirectory = "tools";

        public UninstallToolCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().Build();
            _shellShimRepositoryMock = new ShellShimRepositoryMock(new DirectoryPath(ShimsDirectory), _fileSystem);
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, ShimsDirectory);
        }

        [Fact]
        public void GivenANonExistentPackageItErrors()
        {
            var packageId = "does.not.exist";
            var command = CreateUninstallCommand($"-g {packageId}");

            command.Execute().Should().Be(1);
            _reporter.Lines.Count.Should().Be(1);

            _reporter
                .Lines[0]
                .Should()
                .Be(string.Format(
                    LocalizableStrings.ToolNotInstalled,
                    packageId).Red());
        }

        [Fact]
        public void GivenAMissingGlobalOptionItErrors()
        {
            var command = CreateUninstallCommand("does.not.exist");

            Action a = () => {
                command.Execute();
            };

            a.ShouldThrow<GracefulException>()
             .And
             .Message
             .Should()
             .Be(LocalizableStrings.UninstallToolCommandOnlySupportsGlobal);
        }

        [Fact]
        public void GivenAPackageItUninstalls()
        {
            CreateInstallCommand($"-g {PackageId}").Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    InstallLocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion));

            var packageDirectory = new DirectoryPath(ToolsDirectory).WithSubDirectories(PackageId, PackageVersion);
            var shimPath = Path.Combine(
                ShimsDirectory,
                ProjectRestorerMock.FakeCommandName +
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();

            _reporter.Lines.Clear();

            CreateUninstallCommand($"-g {PackageId}").Execute().Should().Be(0);

            _reporter
                .Lines
                .Single()
                .Should()
                .Contain(string.Format(
                    LocalizableStrings.UninstallSucceeded,
                    PackageId,
                    PackageVersion));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeFalse();
            _fileSystem.File.Exists(shimPath).Should().BeFalse();
        }

        [Fact]
        public void GivenAFailureToUninstallItLeavesItInstalled()
        {
            CreateInstallCommand($"-g {PackageId}").Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    InstallLocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion));

            var packageDirectory = new DirectoryPath(ToolsDirectory).WithSubDirectories(PackageId, PackageVersion);
            var shimPath = Path.Combine(
                ShimsDirectory,
                ProjectRestorerMock.FakeCommandName +
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();

            _reporter.Lines.Clear();

            CreateUninstallCommand(
                options: $"-g {PackageId}",
                uninstallCallback: () => throw new IOException("simulated error"))
                .Execute()
                .Should()
                .Be(1);

            _reporter
                .Lines
                .Single()
                .Should()
                .Contain(string.Format(
                    CommonLocalizableStrings.FailedToUninstallToolPackage,
                    PackageId,
                    "simulated error"));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();
        }

        private InstallToolCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet install tool " + options);

            var store = new ToolPackageStoreMock(new DirectoryPath(ToolsDirectory), _fileSystem);
            return new InstallToolCommand(
                result["dotnet"]["install"]["tool"],
                result,
                store,
                new ToolPackageInstallerMock(
                    _fileSystem,
                    store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter)),
                _shellShimRepositoryMock,
                _environmentPathInstructionMock,
                _reporter);
        }

        private UninstallToolCommand CreateUninstallCommand(string options, Action uninstallCallback = null)
        {
            ParseResult result = Parser.Instance.Parse("dotnet uninstall tool " + options);

            return new UninstallToolCommand(
                result["dotnet"]["uninstall"]["tool"],
                result,
                new ToolPackageStoreMock(
                    new DirectoryPath(ToolsDirectory),
                    _fileSystem,
                    uninstallCallback),
                _shellShimRepositoryMock,
                _reporter);
        }
    }
}
