// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands
{
    public class ToolUpdateCommandTests
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ToolPackageStoreMock _store;
        private readonly PackageId _packageId = new PackageId("global.tool.console.demo");
        private readonly List<MockFeed> _mockFeeds;
        private const string LowerPackageVersion = "1.0.4";
        private const string HigherPackageVersion = "1.0.5";
        private const string ShimsDirectory = "shims";
        private const string ToolsDirectory = "tools";

        public ToolUpdateCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().Build();
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, ShimsDirectory);
            _store = new ToolPackageStoreMock(new DirectoryPath(ToolsDirectory), _fileSystem);
            _mockFeeds = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.FeedFromLookUpNugetConfig,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = LowerPackageVersion
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = HigherPackageVersion
                        }
                    }
                }
            };
        }

        [Fact]
        public void GivenANonExistentPackageItErrors()
        {
            var packageId = "does.not.exist";
            var command = CreateUpdateCommand($"-g {packageId}");

            Action a = () => command.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.ToolNotInstalled,
                        packageId));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanPrintSucessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallItCanPrintSucessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceededVersionNoChange,
                _packageId, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsIthasTheFirstLineIndicateUpdateFailure()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");
            var command = new ToolUpdateCommand(
                result["dotnet"]["tool"]["update"],
                result,
                _ => (_store,
                    new ToolPackageInstallerMock(
                        _fileSystem,
                        _store,
                        new ProjectRestorerMock(
                            _fileSystem,
                            _reporter,
                            _mockFeeds
                        ),
                        installCallback: () => throw new ToolConfigurationException("Simulated error"))),
                _ => new ShellShimRepositoryMock(new DirectoryPath(ShimsDirectory), _fileSystem),
                _reporter);

            Action a = () => command.Execute();
            a.ShouldThrow<GracefulException>().And.Message.Should().Contain(
                string.Format(LocalizableStrings.UpdateToolFailed, _packageId) + Environment.NewLine +
                string.Format(Tools.Tool.Install.LocalizableStrings.InvalidToolConfiguration, "Simulated error"));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsItRollsBack()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");
            var command = new ToolUpdateCommand(
                result["dotnet"]["tool"]["update"],
                result,
                _ => (_store,
                    new ToolPackageInstallerMock(
                        _fileSystem,
                        _store,
                        new ProjectRestorerMock(
                            _fileSystem,
                            _reporter,
                            _mockFeeds
                        ),
                        installCallback: () => throw new ToolConfigurationException("Simulated error"))),
                _ => new ShellShimRepositoryMock(new DirectoryPath(ShimsDirectory), _fileSystem),
                _reporter);

            Action a = () => command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(LowerPackageVersion);
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var command = CreateUpdateCommand($"-g --tool-path /tmp/folder {_packageId}");

            Action a = () => command.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    LocalizableStrings.UpdateToolCommandInvalidGlobalAndToolPath);
        }

        [Fact]
        public void WhenRunWithNeitherOfGlobalNorToolPathShowErrorMessage()
        {
            var command = CreateUpdateCommand($"{_packageId}");

            Action a = () => command.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    LocalizableStrings.UpdateToolCommandNeedGlobalOrToolPath);
        }

        private ToolInstallCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool install " + options);

            return new ToolInstallCommand(
                result["dotnet"]["tool"]["install"],
                result,
                (_) => (_store, new ToolPackageInstallerMock(
                    _fileSystem,
                    _store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter,
                        _mockFeeds
                    ))),
                (_) => new ShellShimRepositoryMock(new DirectoryPath(ShimsDirectory), _fileSystem),
                _environmentPathInstructionMock,
                _reporter);
        }

        private ToolUpdateCommand CreateUpdateCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool update " + options);

            return new ToolUpdateCommand(
                result["dotnet"]["tool"]["update"],
                result,
                (_) => (_store, new ToolPackageInstallerMock(
                    _fileSystem,
                    _store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter,
                        _mockFeeds
                    ))),
                (_) => new ShellShimRepositoryMock(new DirectoryPath(ShimsDirectory), _fileSystem),
                _reporter);
        }
    }
}
