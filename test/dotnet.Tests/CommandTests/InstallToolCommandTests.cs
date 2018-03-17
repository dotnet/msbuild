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
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Install.Tool;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.Runtime.InteropServices;
using LocalizableStrings = Microsoft.DotNet.Tools.Install.Tool.LocalizableStrings;
using Microsoft.DotNet.ShellShim;

namespace Microsoft.DotNet.Tests.Commands
{
    public class InstallToolCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoreAndInstaller _createToolPackageStoreAndInstaller;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private const string PathToPlaceShim = "pathToPlace";
        private const string PathToPlacePackages = PathToPlaceShim + "pkg";
        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";

        public InstallToolCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().Build();
            _toolPackageStore = new ToolPackageStoreMock(new DirectoryPath(PathToPlacePackages), _fileSystem);
            _createShellShimRepository =
                (nonGlobalLocation) => new ShellShimRepositoryMock(new DirectoryPath(PathToPlaceShim), _fileSystem);
            _environmentPathInstructionMock =
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim);
            _createToolPackageStoreAndInstaller = (_) => (_toolPackageStore, CreateToolPackageInstaller());

            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId}");
            _appliedCommand = result["dotnet"]["install"]["tool"];
            var parser = Parser.Instance;
            _parseResult = parser.ParseFrom("dotnet install", new[] {"tool", PackageId});
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldCreateValidShim()
        {
            var installToolCommand = new InstallToolCommand(_appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath()).Should().BeTrue();
            var deserializedFakeShim = JsonConvert.DeserializeObject<ShellShimRepositoryMock.FakeShim>(
                _fileSystem.File.ReadAllText(ExpectedCommandPath()));

            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdWithSourceItShouldCreateValidShim()
        {
            const string sourcePath = "http://mysouce.com";
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --source {sourcePath}");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];
            ParseResult parseResult =
                Parser.Instance.ParseFrom("dotnet install", new[] { "tool", PackageId, "--source", sourcePath });


            var toolToolPackageInstaller = CreateToolPackageInstaller(
            feeds: new MockFeed[] {
                    new MockFeed
                    {
                        Type = MockFeedType.Source,
                        Uri = sourcePath,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = PackageId,
                                Version = PackageVersion
                            }
                        }
                    }
            });

            var installToolCommand = new InstallToolCommand(appliedCommand,
                parseResult,
                (_) => (_toolPackageStore, toolToolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath())
            .Should().BeTrue();
            var deserializedFakeShim =
                JsonConvert.DeserializeObject<ShellShimRepositoryMock.FakeShim>(
                    _fileSystem.File.ReadAllText(ExpectedCommandPath()));
            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowPathInstruction()
        {
            var installToolCommand = new InstallToolCommand(_appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void GivenFailedPackageInstallWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageInstaller =
                CreateToolPackageInstaller(
                    installCallback: () => throw new ToolPackageException("Simulated error"));

            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                (_) => (_toolPackageStore, toolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    "Simulated error" + Environment.NewLine
                                      + string.Format(LocalizableStrings.ToolInstallationFailed, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(PathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenCreateShimItShouldHaveNoBrokenFolderOnDisk()
        {
            _fileSystem.File.CreateEmptyFile(ExpectedCommandPath()); // Create conflict shim

            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CommonLocalizableStrings.ShellShimConflict,
                    ProjectRestorerMock.FakeCommandName));

            _fileSystem.Directory.Exists(Path.Combine(PathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenInCorrectToolConfigurationWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageInstaller =
            CreateToolPackageInstaller(
                installCallback: () => throw new ToolConfigurationException("Simulated error"));

            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                (_) => (_toolPackageStore, toolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.InvalidToolConfiguration,
                        "Simulated error") + Environment.NewLine +
                    string.Format(LocalizableStrings.ToolInstallationFailedContactAuthor, PackageId)
                );
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithInvalidVersionItShouldThrow()
        {
            const string invalidVersion = "!NotValidVersion!";
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --version {invalidVersion}");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            Action action = () => installToolCommand.Execute();

            action
                .ShouldThrow<GracefulException>()
                .WithMessage(string.Format(
                    LocalizableStrings.InvalidNuGetVersionRange,
                    invalidVersion));
        }

        [Fact]
        public void WhenRunWithExactVersionItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --version {PackageVersion}");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithValidVersionRangeItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --version [1.0,2.0]");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithoutAMatchingRangeItShouldFail()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --version [5.0,10.0]");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    LocalizableStrings.ToolInstallationRestoreFailed +
                    Environment.NewLine + string.Format(LocalizableStrings.ToolInstallationFailed, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(PathToPlacePackages, PackageId)).Should().BeFalse();
        }

         [Fact]
        public void WhenRunWithValidVersionWildcardItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g {PackageId} --version 1.0.*");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.FakeCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet install tool -g --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["install"]["tool"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet install", new[] {"tool", PackageId});

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.InstallToolCommandInvalidGlobalAndToolPath);
        }

        [Fact]
        public void WhenRunWithNeitherOfGlobalNorToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet install tool {PackageId}");
            var appliedCommand = result["dotnet"]["install"]["tool"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet install", new[] { "tool", PackageId });

            var installToolCommand = new InstallToolCommand(
                appliedCommand,
                parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            Action a = () => installToolCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(LocalizableStrings.InstallToolCommandNeedGlobalOrToolPath);
        }

        [Fact]
        public void WhenRunWithPackageIdAndBinPathItShouldNoteHaveEnvironmentPathInstruction()
        {
            var result = Parser.Instance.Parse($"dotnet install tool --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["install"]["tool"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet install", new[] {"tool", PackageId});

            var installToolCommand = new InstallToolCommand(appliedCommand,
                parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().NotContain(l => l.Contains(EnvironmentPathInstructionMock.MockInstructionText));
        }

        private IToolPackageInstaller CreateToolPackageInstaller(
            IEnumerable<MockFeed> feeds = null,
            Action installCallback = null)
        {
            return new ToolPackageInstallerMock(
                fileSystem: _fileSystem,
                store: _toolPackageStore,
                projectRestorer: new ProjectRestorerMock(
                    fileSystem: _fileSystem,
                    reporter: _reporter,
                    feeds: feeds),
                installCallback: installCallback);
        }

        private static string ExpectedCommandPath()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            return Path.Combine(
                "pathToPlace",
                ProjectRestorerMock.FakeCommandName + extension);
        }
    }
}
