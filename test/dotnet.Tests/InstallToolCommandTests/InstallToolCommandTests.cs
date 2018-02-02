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
using Microsoft.DotNet.Tools.Install.Tool;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.Install.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Tests.InstallToolCommandTests
{
    public class InstallToolCommandTests
    {
        private readonly IFileSystem _fileSystemWrapper;
        private readonly ToolPackageObtainerMock _toolPackageObtainerMock;
        private readonly ShellShimMakerMock _shellShimMakerMock;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private const string PathToPlaceShim = "pathToPlace";

        public InstallToolCommandTests()
        {
            _fileSystemWrapper = new FileSystemMockBuilder().Build();
            _toolPackageObtainerMock = new ToolPackageObtainerMock(_fileSystemWrapper);
            _shellShimMakerMock = new ShellShimMakerMock(PathToPlaceShim, _fileSystemWrapper);
            _reporter = new BufferedReporter();
            _environmentPathInstructionMock =
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim);

            ParseResult result = Parser.Instance.Parse("dotnet install tool -g global.tool.console.demo");
            _appliedCommand = result["dotnet"]["install"]["tool"];
            var parser = Parser.Instance;
            _parseResult = parser.ParseFrom("dotnet install", new[] {"tool", "global.tool.console.demo"});
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldCreateValidShim()
        {
            var installToolCommand = new InstallToolCommand(_appliedCommand,
                _parseResult,
                _toolPackageObtainerMock,
                _shellShimMakerMock,
                _environmentPathInstructionMock);

            installToolCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystemWrapper.File.Exists(Path.Combine("pathToPlace", ToolPackageObtainerMock.FakeCommandName))
                .Should().BeTrue();
            var deserializedFakeShim = JsonConvert.DeserializeObject<ShellShimMakerMock.FakeShim>(
                _fileSystemWrapper.File.ReadAllText(
                    Path.Combine("pathToPlace",
                        ToolPackageObtainerMock.FakeCommandName)));
            _fileSystemWrapper.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdWithSourceItShouldCreateValidShim()
        {
            const string sourcePath = "http://mysouce.com";
            ParseResult result = Parser.Instance.Parse($"dotnet install tool -g global.tool.console.demo --source {sourcePath}");
            AppliedOption appliedCommand = result["dotnet"]["install"]["tool"];
            const string packageId = "global.tool.console.demo";
            ParseResult parseResult =
                Parser.Instance.ParseFrom("dotnet install", new[] {"tool", packageId, "--source", sourcePath});

            var installToolCommand = new InstallToolCommand(appliedCommand,
                parseResult,
                new ToolPackageObtainerMock(_fileSystemWrapper, additionalFeeds: new List<MockFeed>
                {
                    new MockFeed
                    {
                        Type = MockFeedType.Source,
                        Uri = sourcePath,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = packageId,
                                Version = "1.0.4"
                            }
                        }
                    }
                }),
                _shellShimMakerMock,
                _environmentPathInstructionMock);

            installToolCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystemWrapper.File.Exists(Path.Combine("pathToPlace", ToolPackageObtainerMock.FakeCommandName))
                .Should().BeTrue();
            ShellShimMakerMock.FakeShim deserializedFakeShim =
                JsonConvert.DeserializeObject<ShellShimMakerMock.FakeShim>(
                    _fileSystemWrapper.File.ReadAllText(
                        Path.Combine("pathToPlace",
                            ToolPackageObtainerMock.FakeCommandName)));
            _fileSystemWrapper.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowPathInstruction()
        {
            var installToolCommand = new InstallToolCommand(_appliedCommand,
                _parseResult,
                _toolPackageObtainerMock,
                _shellShimMakerMock,
                _environmentPathInstructionMock);

            installToolCommand.Execute().Should().Be(0);

            _reporter.Lines.Single().Should().NotBeEmpty();
        }

        [Fact]
        public void GivenFailedPackageObtainWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageObtainerSimulatorThatThrows
                = new ToolPackageObtainerMock(
                    _fileSystemWrapper, true, null,
                    () => throw new PackageObtainException("Simulated error"));
            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                toolPackageObtainerSimulatorThatThrows,
                _shellShimMakerMock,
                _environmentPathInstructionMock,
                _reporter);

            installToolCommand.Execute().Should().Be(1);

            _reporter.Lines.Count.Should().Be(2);

            _reporter
                .Lines[0]
                .Should()
                .Contain("Simulated error");

            _reporter
                .Lines[1]
                .Should()
                .Contain(string.Format(LocalizableStrings.ToolInstallationFailed, "global.tool.console.demo"));
        }

        [Fact]
        public void GivenInCorrectToolConfigurationWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageObtainerSimulatorThatThrows
                = new ToolPackageObtainerMock(
                    _fileSystemWrapper, true, null,
                    () => throw new ToolConfigurationException("Simulated error"));
            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                toolPackageObtainerSimulatorThatThrows,
                _shellShimMakerMock,
                _environmentPathInstructionMock,
                _reporter);

            installToolCommand.Execute().Should().Be(1);

            _reporter.Lines.Count.Should().Be(2);

            _reporter
                .Lines[0]
                .Should()
                .Contain(
                    string.Format(
                        LocalizableStrings.InvalidToolConfiguration,
                        "Simulated error"));

            _reporter
                .Lines[1]
                .Should()
                .Contain(string.Format(LocalizableStrings.ToolInstallationFailedContactAuthor, "global.tool.console.demo"));
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            var installToolCommand = new InstallToolCommand(
                _appliedCommand,
                _parseResult,
                _toolPackageObtainerMock,
                _shellShimMakerMock,
                new EnvironmentPathInstructionMock(_reporter, PathToPlaceShim, true),
                _reporter);

            installToolCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Single().Should()
                .Contain(string.Format(LocalizableStrings.InstallationSucceeded, "SimulatorCommand"));
        }
    }
}
