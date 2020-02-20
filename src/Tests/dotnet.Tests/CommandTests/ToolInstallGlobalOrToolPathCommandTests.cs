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
using System.Text.Json;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallGlobalOrToolPathCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly IToolPackageStoreQuery _toolPackageStoreQuery;
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndInstaller _createToolPackageStoreAndInstaller;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlaceShim;
        private readonly string _pathToPlacePackages;
        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private const string ToolCommandName = "SimulatorCommand";

        public ToolInstallGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory =  _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _pathToPlaceShim = Path.Combine(_temporaryDirectory, "pathToPlace");
            _fileSystem.Directory.CreateDirectory(_pathToPlaceShim);
            _pathToPlacePackages = _pathToPlaceShim + "Packages";
            var toolPackageStoreMock = new ToolPackageStoreMock(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;
            _toolPackageStoreQuery = toolPackageStoreMock;
            _createShellShimRepository =
                (nonGlobalLocation) => new ShellShimRepository(
                    new DirectoryPath(_pathToPlaceShim),
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem),
                    filePermissionSetter: new NoOpFilePermissionSetter());
            _environmentPathInstructionMock =
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim);
            _createToolPackageStoreAndInstaller = (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, CreateToolPackageInstaller());

            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId}");
            _appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            _parseResult = parser.ParseFrom("dotnet tool", new[] {"install", "-g", PackageId});
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldCreateValidShim()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(_appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath()).Should().BeTrue();
            var deserializedFakeShim = JsonSerializer.Deserialize<AppHostShellShimMakerMock.FakeShim>(
                _fileSystem.File.ReadAllText(ExpectedCommandPath()));

            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunFromToolInstallRedirectCommandWithPackageIdItShouldCreateValidShim()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(_appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            var toolInstallCommand = new ToolInstallCommand(
                _appliedCommand,
                _parseResult,
                toolInstallGlobalOrToolPathCommand);

            toolInstallCommand.Execute().Should().Be(0);

            _fileSystem.File.Exists(ExpectedCommandPath()).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdWithSourceItShouldCreateValidShim()
        {
            const string sourcePath = "http://mysource.com";
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --add-source {sourcePath}");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];
            ParseResult parseResult =
                Parser.Instance.ParseFrom("dotnet tool", new[] { "install", "-g", PackageId, "--add-source", sourcePath });


            var toolToolPackageInstaller = CreateToolPackageInstaller(
            feeds: new List<MockFeed> {
                    new MockFeed
                    {
                        Type = MockFeedType.ImplicitAdditionalFeed,
                        Uri = sourcePath,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = PackageId,
                                Version = PackageVersion,
                                ToolCommandName = ToolCommandName,
                            }
                        }
                    }
            });

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(appliedCommand,
                parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolToolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath())
            .Should().BeTrue();
            var deserializedFakeShim =
                JsonSerializer.Deserialize<AppHostShellShimMakerMock.FakeShim>(
                    _fileSystem.File.ReadAllText(ExpectedCommandPath()));
            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowPathInstruction()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(_appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void WhenRunWithPackageIdPackageFormatIsNotFullySupportedItShouldShowPathInstruction()
        {
            const string Warning = "WARNING";
            var injectedWarnings = new Dictionary<PackageId, IEnumerable<string>>()
            {
                [new PackageId(PackageId)] = new List<string>() { Warning }
            };

            var toolPackageInstaller = new ToolPackageInstallerMock(
                fileSystem: _fileSystem,
                store: _toolPackageStore,
                projectRestorer: new ProjectRestorerMock(
                    fileSystem: _fileSystem,
                    reporter: _reporter),
                warningsMap: injectedWarnings);

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _appliedCommand,
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(Warning.Yellow());
            _reporter.Lines.Skip(1).First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void GivenFailedPackageInstallWhenRunWithPackageIdItShouldFail()
        {
            const string ErrorMessage = "Simulated error";

            var toolPackageInstaller =
                CreateToolPackageInstaller(
                    installCallback: () => throw new ToolPackageException(ErrorMessage));

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _appliedCommand,
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    ErrorMessage +
                    Environment.NewLine +
                    string.Format(LocalizableStrings.ToolInstallationFailedWithRestoreGuidance, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenCreateShimItShouldHaveNoBrokenFolderOnDisk()
        {
            _fileSystem.File.CreateEmptyFile(ExpectedCommandPath()); // Create conflict shim

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CommonLocalizableStrings.ShellShimConflict,
                    ToolCommandName));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenInCorrectToolConfigurationWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageInstaller =
            CreateToolPackageInstaller(
                installCallback: () => throw new ToolConfigurationException("Simulated error"));

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _appliedCommand,
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageInstaller),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

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
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _appliedCommand,
                _parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithInvalidVersionItShouldThrow()
        {
            const string invalidVersion = "!NotValidVersion!";
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version {invalidVersion}");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action action = () => toolInstallGlobalOrToolPathCommand.Execute();

            action
                .ShouldThrow<GracefulException>()
                .WithMessage(string.Format(
                    LocalizableStrings.InvalidNuGetVersionRange,
                    invalidVersion));
        }

        [Fact]
        public void WhenRunWithExactVersionItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithValidVersionRangeItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version [1.0,2.0]");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithoutAMatchingRangeItShouldFail()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version [5.0,10.0]");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    LocalizableStrings.ToolInstallationRestoreFailed +
                    Environment.NewLine + string.Format(LocalizableStrings.ToolInstallationFailedWithRestoreGuidance, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

         [Fact]
        public void WhenRunWithValidVersionWildcardItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version 1.0.*");
            AppliedOption appliedCommand = result["dotnet"]["tool"]["install"];

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                appliedCommand,
                result,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithPackageIdAndBinPathItShouldNoteHaveEnvironmentPathInstruction()
        {
            var result = Parser.Instance.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet tool", new[] {"install", "-g", PackageId});

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(appliedCommand,
                parseResult,
                _createToolPackageStoreAndInstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().NotContain(l => l.Contains(EnvironmentPathInstructionMock.MockInstructionText));
        }

        [Fact]
        public void AndPackagedShimIsProvidedWhenRunWithPackageIdItCreateShimUsingPackagedShim()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            var prepackagedShimPath = Path.Combine (_temporaryDirectory, ToolCommandName + extension);
            var tokenToIdentifyPackagedShim = "packagedShim";
            _fileSystem.File.WriteAllText(prepackagedShimPath, tokenToIdentifyPackagedShim);

            var result = Parser.Instance.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");
            var appliedCommand = result["dotnet"]["tool"]["install"];
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet tool", new[] {"install", "-g", PackageId});

            var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
            {
                [new PackageId(PackageId)] = new[] {new FilePath(prepackagedShimPath)}
            };

            var installCommand = new ToolInstallGlobalOrToolPathCommand(appliedCommand,
                parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, new ToolPackageInstallerMock(
                    fileSystem: _fileSystem,
                    store: _toolPackageStore,
                    packagedShimsMap: packagedShimsMap,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: _fileSystem,
                        reporter: _reporter))),
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim),
                _reporter);

            installCommand.Execute().Should().Be(0);

            _fileSystem.File.ReadAllText(ExpectedCommandPath()).Should().Be(tokenToIdentifyPackagedShim);
        }

        private IToolPackageInstaller CreateToolPackageInstaller(
            List<MockFeed> feeds = null,
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

        private string ExpectedCommandPath()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            return Path.Combine(
                _pathToPlaceShim,
                ToolCommandName + extension);
        }

        private class NoOpFilePermissionSetter : IFilePermissionSetter
        {
            public void SetUserExecutionPermission(string path)
            {
            }
        }
    }
}
